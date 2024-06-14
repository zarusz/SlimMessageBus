namespace SlimMessageBus.Host.Outbox.DbContext.Test;

/// <summary>
/// This test should help to understand the runtime performance and overhead of the outbox feature.
/// It will generate the time measurements for a given transport (Azure DB + Azure SQL instance) as the baseline, 
/// and then measure times when outbox is enabled. 
/// This should help asses the overhead of outbox and to baseline future outbox improvements.
/// </summary>
/// <param name="testOutputHelper"></param>
[Trait("Category", "Integration")] // for benchmarks
public class OutboxBenchmarkTests(ITestOutputHelper testOutputHelper) : BaseIntegrationTest<OutboxBenchmarkTests>(testOutputHelper)
{
    private static readonly string OutboxTableName = "IntTest_Benchmark_Outbox";
    private static readonly string MigrationsTableName = "IntTest_Benchmark_Migrations";

    private bool _useOutbox;

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus(mbb =>
        {
            mbb.AddChildBus("ExternalBus", mbb =>
            {
                var topic = "tests.outbox-benchmark/customer-events";
                mbb
                    .WithProviderServiceBus(cfg =>
                    {
                        cfg.ConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);
                        cfg.PrefetchCount = 100; // fetch 100 messages at a time
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
                opts.PollIdleSleep = TimeSpan.FromSeconds(0.5);
                opts.MessageCleanup.Interval = TimeSpan.FromSeconds(10);
                opts.MessageCleanup.Age = TimeSpan.FromMinutes(1);
                opts.SqlSettings.DatabaseTableName = OutboxTableName;
                opts.SqlSettings.DatabaseMigrationsTableName = MigrationsTableName;
            });
            mbb.AutoStartConsumersEnabled(false);
        });

        services.AddSingleton<TestEventCollector<CustomerCreatedEvent>>();

        // Entity Framework setup - application specific EF DbContext
        services.AddDbContext<CustomerContext>(options => options.UseSqlServer(Secrets.Service.PopulateSecrets(Configuration.GetConnectionString("DefaultConnection"))));
    }

    private async Task PerformDbOperation(Func<CustomerContext, Task> action)
    {
        using var scope = ServiceProvider!.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CustomerContext>();
        await action(context);
    }

    [Theory]
    [InlineData([true, 1000])] // compare with outbox
    [InlineData([false, 1000])] // vs. without outbox
    public async Task Given_EventPublisherAndConsumerUsingOutbox_When_BurstOfEventsIsSent_Then_EventsAreConsumedProperly(bool useOutbox, int messageCount)
    {
        // arrange
        _useOutbox = useOutbox;

        await PerformDbOperation(async context =>
        {
            // migrate db
            await context.Database.MigrateAsync();

            // clean outbox from previous test run
            await context.Database.EraseTableIfExists(OutboxTableName);
            await context.Database.EraseTableIfExists(MigrationsTableName);
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

        // start consumers
        await EnsureConsumersStarted();

        // consume the events from outbox
        var consumptionTimer = Stopwatch.StartNew();
        await store.WaitUntilArriving(newMessagesTimeout: 5, expectedCount: events.Count);

        // assert

        var consumeTimerElapsed = consumptionTimer.Elapsed;

        // Log the measured times
        Logger.LogInformation("Message Publish took: {Elapsed}", publishTimerElapsed);
        Logger.LogInformation("Message Consume took: {Elapsed}", consumeTimerElapsed);

        // Ensure the expected number of events was actually published to ASB and delivered via that channel.
        store.Count.Should().Be(events.Count);
    }
}
