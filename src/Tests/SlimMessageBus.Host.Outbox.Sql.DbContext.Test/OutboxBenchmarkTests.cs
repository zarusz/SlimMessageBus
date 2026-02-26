namespace SlimMessageBus.Host.Outbox.Sql.DbContext.Test;

using System.Net.Mime;

using Microsoft.EntityFrameworkCore.Migrations;

using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Outbox;
using SlimMessageBus.Host.Outbox.Services;
using SlimMessageBus.Host.Outbox.Sql.DbContext;
using SlimMessageBus.Host.Outbox.Sql.DbContext.Test.DataAccess;
using SlimMessageBus.Host.RabbitMQ;

/// <summary>
/// This test should help to understand the runtime performance and overhead of the outbox feature.
/// It will generate the time measurements for a given transport (Azure DB + Azure SQL instance) as the baseline, 
/// and then measure times when outbox is enabled. 
/// This should help asses the overhead of outbox and to baseline future outbox improvements.
/// </summary>
/// <param name="output"></param>
[Trait("Category", "Integration")] // for benchmarks
[Trait("Transport", "Outbox")]
[Collection(CustomerContext.Schema)]
public class OutboxBenchmarkTests(ITestOutputHelper output) : BaseOutboxIntegrationTest<OutboxBenchmarkTests>(output)
{
    private bool _useOutbox;
    private BusType _testParamBusType;

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus(mbb =>
        {
            mbb.AutoStartConsumersEnabled(false);

            switch (_testParamBusType)
            {
                case BusType.AzureSB:
                    mbb.AddChildBus("Azure", mbb =>
                    {
                        var topic = $"smb-tests/{nameof(OutboxBenchmarkTests)}/benchmark/{DateTimeOffset.UtcNow.Ticks}";
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
                    break;

                case BusType.RabbitMQ:
                    mbb.AddChildBus("Rabbit", mbb =>
                    {
                        var topic = $"{nameof(OutboxBenchmarkTests)}-{DateTimeOffset.UtcNow.Ticks}";
                        var queue = nameof(CustomerCreatedEvent);

                        mbb.WithProviderRabbitMQ(cfg =>
                        {
                            cfg.ConnectionString = Secrets.Service.PopulateSecrets(configuration["RabbitMQ:ConnectionString"]);
                            cfg.ConnectionFactory.ClientProvidedName = $"{nameof(OutboxBenchmarkTests)}_{Environment.MachineName}";

                            cfg.UseMessagePropertiesModifier((m, p) =>
                            {
                                p.ContentType = MediaTypeNames.Application.Json;
                            });
                            cfg.UseExchangeDefaults(durable: false);
                            cfg.UseQueueDefaults(durable: false);
                            cfg.UseTopologyInitializer(async (channel, applyDefaultTopology) =>
                            {
                                // before test clean up
                                await channel.QueueDeleteAsync(queue, ifUnused: true, ifEmpty: false);
                                await channel.ExchangeDeleteAsync(topic, ifUnused: true);

                                // apply default SMB inferred topology
                                await applyDefaultTopology();

                                // after
                            });
                        })
                        .Produce<CustomerCreatedEvent>(x => x
                            .Exchange(topic, exchangeType: ExchangeType.Fanout, autoDelete: true)
                            .RoutingKeyProvider((m, p) => Guid.NewGuid().ToString()))
                        .Consume<CustomerCreatedEvent>(x => x
                            .Queue(queue, autoDelete: true)
                            .ExchangeBinding(topic)
                            .AcknowledgementMode(RabbitMqMessageAcknowledgementMode.AckAutomaticByRabbit)
                            .WithConsumer<CustomerCreatedEventConsumer>());

                        if (_useOutbox)
                        {
                            mbb
                                .UseOutbox(); // All outgoing messages from this bus will go out via an outbox
                        }
                    });
                    break;
                default:
                    throw new NotSupportedException($"Bus {_testParamBusType} is not configured");
            }

            mbb.AddServicesFromAssembly(Assembly.GetExecutingAssembly());
            mbb.AddJsonSerializer();
            mbb.AddOutboxUsingDbContext<CustomerContext>(opts =>
            {
                opts.PollBatchSize = 500;
                opts.LockExpiration = TimeSpan.FromSeconds(5);
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

    [Theory]
    [InlineData([BusType.AzureSB, true, 1000])]     // compare with outbox
    [InlineData([BusType.RabbitMQ, true, 1000])]
    [InlineData([BusType.AzureSB, false, 1000])]    // vs. without outbox
    [InlineData([BusType.RabbitMQ, false, 1000])]
    public async Task Given_EventPublisherAndConsumerUsingOutbox_When_BurstOfEventsIsSent_Then_EventsAreConsumedProperly(BusType busType, bool useOutbox, int messageCount)
    {
        _testParamBusType = busType;

        // arrange
        _useOutbox = useOutbox;

        await PerformDbOperation(async (context, outboxMigrationService) =>
        {
            // migrate db
            await context.Database.DropSchemaIfExistsAsync(context.Model.GetDefaultSchema());
            await context.Database.MigrateAsync();
        });

        var surnames = new[] { "Doe", "Smith", "Kowalsky" };
        var events = Enumerable.Range(0, messageCount).Select(x => new CustomerCreatedEvent(Guid.NewGuid(), $"John {x:000}", surnames[x % surnames.Length])).ToList();
        var store = ServiceProvider!.GetRequiredService<TestEventCollector<CustomerCreatedEvent>>();

        OutboxSendingTask<SqlOutboxMessage> outboxSendingTask = null;
        if (_useOutbox)
        {
            outboxSendingTask = ServiceProvider.GetRequiredService<OutboxSendingTask<SqlOutboxMessage>>();

            // migrate data context
            await outboxSendingTask.OnBusLifecycle(Interceptor.MessageBusLifecycleEventType.Created, null);

            // place service into an impossible state (-1 instances) so that:
            // * the service will not start when the bus starts
            // * outbox notifications will not be acted upon
            await (outboxSendingTask.OnBusLifecycle(Interceptor.MessageBusLifecycleEventType.Stopping, null) ?? Task.CompletedTask);
        }

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
                        await bus.Publish(ev, headers: new Dictionary<string, object> { ["CustomerId"] = ev.Id.ToString() });
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
            var outputRepository = ServiceProvider.GetRequiredService<IOutboxMessageRepository<SqlOutboxMessage>>();

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
        await store.WaitUntilArriving(newMessagesTimeout: 15, expectedCount: events.Count);

        // assert
        var consumeTimerElapsed = consumptionTimer.Elapsed;

        // Log the measured times
        Logger.LogInformation("Message Publish took       : {Elapsed}", publishTimerElapsed);
        Logger.LogInformation("Outbox Publish took        : {Elapsed}", outboxPublishTimerElapsed);
        Logger.LogInformation("Message Consume took       : {Elapsed}", consumeTimerElapsed);

        // Ensure the expected number of events was actually published to ASB and delivered via that channel.
        store.Count.Should().Be(events.Count);
    }
}
