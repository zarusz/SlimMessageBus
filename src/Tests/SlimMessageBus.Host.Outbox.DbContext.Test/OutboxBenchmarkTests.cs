namespace SlimMessageBus.Host.Outbox.DbContext.Test;

using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// This test should help to understand the runtime performance and overhead of the outbox feature.
/// It will generate the time measurements for a given transport (Azure DB + Azure SQL instance) as the baseline, 
/// and then measure times when outbox is enabled. 
/// This should help asses the overhead of outbox and to baseline future outbox improvements.
/// </summary>
/// <param name="testOutputHelper"></param>
[Trait("Category", "Integration")] // for benchmarks
[Collection(CustomerContext.Schema)]
public class OutboxBenchmarkTests(ITestOutputHelper testOutputHelper) : BaseIntegrationTest<OutboxBenchmarkTests>(testOutputHelper)
{
    private bool _useOutbox;

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus(mbb =>
        {
            mbb.AutoStartConsumersEnabled(false);

            mbb.AddChildBus("ExternalBus", mbb =>
            {
                var topic = $"smb-tests/outbox-benchmark/{Guid.NewGuid():N}/customer-events";
                mbb
                    .WithProviderServiceBus(cfg =>
                    {
                        cfg.ConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);
                        cfg.PrefetchCount = 100; // fetch 100 messages at a time
                        cfg.TopologyProvisioning.CreateTopicOptions = o => o.AutoDeleteOnIdle = TimeSpan.FromMinutes(5);
                    })
                    .Produce<CustomerCreatedEvent>(x => x.DefaultTopic(topic))
                    .Consume<CustomerCreatedEvent>(x => x
                        .Topic(topic)
                        .WithConsumer<CustomerCreatedEventConsumer>()
                        .Instances(20) // process messages in parallel
                        .SubscriptionName(nameof(OutboxBenchmarkTests))); // for AzureSB

                if (_useOutbox)
                {
                    mbb
                        .UseOutbox(); // All outgoing messages from this bus will go out via an outbox
                }
            });
            mbb.AddServicesFromAssembly(Assembly.GetExecutingAssembly());
            mbb.AddJsonSerializer();
            mbb.AddOutboxUsingDbContext<CustomerContext>(opts =>
            {
                opts.PollBatchSize = 100;
                opts.LockExpiration = TimeSpan.FromMinutes(5);
                opts.PollIdleSleep = TimeSpan.FromDays(1);
                opts.MessageCleanup.Interval = TimeSpan.FromSeconds(10);
                opts.MessageCleanup.Age = TimeSpan.FromMinutes(1);
                opts.SqlSettings.DatabaseSchemaName = CustomerContext.Schema;
            });
        });

        services.AddSingleton<TestEventCollector<CustomerCreatedEvent>>();

        // Entity Framework setup - application specific EF DbContext
        services.AddDbContext<CustomerContext>(
            options => options.UseSqlServer(
                Secrets.Service.PopulateSecrets(Configuration.GetConnectionString("DefaultConnection")),
                x => x.MigrationsHistoryTable(HistoryRepository.DefaultTableName, CustomerContext.Schema)));
    }

    private async Task PerformDbOperation(Func<CustomerContext, IOutboxMigrationService, Task> action)
    {
        var scope = ServiceProvider!.CreateScope();
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<CustomerContext>();
            var outboxMigrationService = scope.ServiceProvider.GetRequiredService<IOutboxMigrationService>();
            await action(context, outboxMigrationService);
        }
        finally
        {
            await ((IAsyncDisposable)scope).DisposeAsync();
        }
    }

    [Theory]
    [InlineData([true, 1000])] // compare with outbox
    [InlineData([false, 1000])] // vs. without outbox
    public async Task Given_EventPublisherAndConsumerUsingOutbox_When_BurstOfEventsIsSent_Then_EventsAreConsumedProperly(bool useOutbox, int messageCount)
    {
        // arrange
        _useOutbox = useOutbox;

        await PerformDbOperation(async (context, outboxMigrationService) =>
        {
            // migrate db
            await context.Database.DropSchemaIfExistsAsync(context.Model.GetDefaultSchema());
            await context.Database.MigrateAsync();

            // migrate outbox sql
            await outboxMigrationService.Migrate(CancellationToken.None);
        });

        var surnames = new[] { "Doe", "Smith", "Kowalsky" };
        var events = Enumerable.Range(0, messageCount).Select(x => new CustomerCreatedEvent(Guid.NewGuid(), $"John {x:000}", surnames[x % surnames.Length])).ToList();
        var store = ServiceProvider!.GetRequiredService<TestEventCollector<CustomerCreatedEvent>>();

        // act

        // publish the events in one shot (consumers are not started yet)
        var publishTimer = Stopwatch.StartNew();

        var publishTasks = events
            .Select(async ev =>
            {
                var unitOfWorkScope = ServiceProvider!.CreateScope();
                await using (unitOfWorkScope as IAsyncDisposable)
                {
                    var bus = unitOfWorkScope.ServiceProvider.GetRequiredService<IMessageBus>();
                    try
                    {
                        await bus.Publish(ev, headers: new Dictionary<string, object> { ["CustomerId"] = ev.Id });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogInformation("Exception occurred while publishing event {Event}: {Message}", ev, ex.Message);
                    }
                }
            })
            .ToArray();

        await Task.WhenAll(publishTasks);

        var publishTimerElapsed = publishTimer.Elapsed;

        // publish messages
        var outboxPublishTimerElapsed = TimeSpan.Zero;
        if (_useOutbox)
        {
            var outboxSendingTask = ServiceProvider.GetRequiredService<OutboxSendingTask>();
            var outputRepository = ServiceProvider.GetRequiredService<IOutboxRepository>();

            var outboxTimer = Stopwatch.StartNew();
            var publishCount = await outboxSendingTask.SendMessages(ServiceProvider, outputRepository, CancellationToken.None);
            outboxPublishTimerElapsed = outboxTimer.Elapsed;

            publishCount.Should().Be(messageCount);
        }

        store.Clear();

        // start consumers
        await EnsureConsumersStarted();

        // consume the events from outbox
        var consumptionTimer = Stopwatch.StartNew();
        await store.WaitUntilArriving(newMessagesTimeout: 5, expectedCount: events.Count);

        // assert
        var consumeTimerElapsed = consumptionTimer.Elapsed;

        // Log the measured times
        Logger.LogInformation("Message Publish took       : {Elapsed}", publishTimerElapsed);
        Logger.LogInformation("Outbox publish took        : {Elapsed}", outboxPublishTimerElapsed);
        Logger.LogInformation("Message Consume took       : {Elapsed}", consumeTimerElapsed);

        // Ensure the expected number of events was actually published to ASB and delivered via that channel.
        store.Count.Should().Be(events.Count);
    }
}
