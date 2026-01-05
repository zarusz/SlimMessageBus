namespace SlimMessageBus.Host.Outbox.Sql.DbContext.Test;

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;

using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Outbox;
using SlimMessageBus.Host.Outbox.Sql.DbContext;
using SlimMessageBus.Host.Outbox.Sql.DbContext.Test.DataAccess;
using SlimMessageBus.Host.Sql.Common;

[Trait("Category", "Integration")]
[Trait("Transport", "Outbox")]
[Collection(CustomerContext.Schema)]
public class OutboxTests(ITestOutputHelper output) : BaseOutboxIntegrationTest<OutboxTests>(output)
{
    private bool _testParamUseHybridBus;
    private TransactionType _testParamTransactionType;
    private BusType _testParamBusType;
    private SqlOutboxMessageIdGenerationMode _testParamIdGenerationMode;

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        void ConfigureExternalBus(MessageBusBuilder mbb)
        {
            var topic = "test-ping";
            if (_testParamBusType == BusType.Kafka)
            {
                var kafkaBrokers = Secrets.Service.PopulateSecrets(configuration["Kafka:Brokers"]);
                var kafkaUsername = Secrets.Service.PopulateSecrets(configuration["Kafka:Username"]);
                var kafkaPassword = Secrets.Service.PopulateSecrets(configuration["Kafka:Password"]);

                mbb.WithProviderKafka(cfg =>
                {
                    cfg.BrokerList = kafkaBrokers;
                    cfg.ProducerConfig = (config) =>
                    {
                        config.LingerMs = 5; // 5ms
                        config.SocketNagleDisable = true;
                    };
                    cfg.ConsumerConfig = (config) =>
                    {
                        config.FetchErrorBackoffMs = 1;
                        config.SocketNagleDisable = true;
                        // when the test containers start there is no consumer group yet, so we want to start from the beginning
                        config.AutoOffsetReset = AutoOffsetReset.Earliest;
                    };
                });
            }
            if (_testParamBusType == BusType.AzureSB)
            {
                mbb.WithProviderServiceBus(cfg =>
                {
                    cfg.ConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);
                    cfg.PrefetchCount = 100;
                    cfg.TopologyProvisioning.CreateTopicOptions = o => o.AutoDeleteOnIdle = TimeSpan.FromMinutes(5);
                });
                topic = $"smb-tests/{nameof(OutboxTests)}/outbox/{DateTimeOffset.UtcNow.Ticks}";
            }

            mbb
                .Produce<CustomerCreatedEvent>(x => x.DefaultTopic(topic))
                .Consume<CustomerCreatedEvent>(x => x
                    .Topic(topic)
                    .Instances(20) // process messages in parallel
                    .SubscriptionName(nameof(OutboxTests)) // for AzureSB
                    .KafkaGroup("subscriber")) // for Kafka
                .UseOutbox(); // All outgoing messages from this bus will go out via an outbox
        }

        services.AddSlimMessageBus(mbb =>
        {
            if (!_testParamUseHybridBus)
            {
                // we test outbox without hybrid bus setup
                ConfigureExternalBus(mbb);
            }
            else
            {
                mbb.PerMessageScopeEnabled(true);

                // we test outbox with hybrid bus setup
                mbb.AddChildBus("Memory", mbb =>
                {
                    mbb.WithProviderMemory()
                        .AutoDeclareFrom(Assembly.GetExecutingAssembly(), consumerTypeFilter: t => t.Name.Contains("Command"));

                    if (_testParamTransactionType == TransactionType.SqlTransaction)
                    {
                        mbb.UseSqlTransaction(); // Consumers/Handlers will be wrapped in a SqlTransaction
                    }
                    if (_testParamTransactionType == TransactionType.TransactionScope)
                    {
                        mbb.UseTransactionScope(); // Consumers/Handlers will be wrapped in a TransactionScope
                    }
                });

                mbb.AddChildBus("ExternalBus", ConfigureExternalBus);
            }
            mbb.AddServicesFromAssembly(Assembly.GetExecutingAssembly());
            mbb.AddJsonSerializer();
            mbb.AddOutboxUsingDbContext<CustomerContext>(opts =>
            {
                opts.PollBatchSize = 100;
                // Reduce poll interval for faster test execution
                opts.PollIdleSleep = TimeSpan.FromMilliseconds(250);
                opts.MessageCleanup.Interval = TimeSpan.FromSeconds(10);
                opts.MessageCleanup.Age = TimeSpan.FromMinutes(1);
                opts.SqlSettings.DatabaseSchemaName = CustomerContext.Schema;
                opts.IdGeneration.Mode = _testParamIdGenerationMode;
                // We want to see the time output in the logs
                opts.MeasureSqlOperations = true;
            });
        });

        services.AddSingleton<TestEventCollector<CustomerCreatedEvent>>();

        // Entity Framework setup - application specific EF DbContext
        services.AddDbContext<CustomerContext>(options => options
            .UseSqlServer(
                Secrets.Service.PopulateSecrets(Configuration.GetConnectionString("DefaultConnection")),
                x => x.MigrationsHistoryTable(HistoryRepository.DefaultTableName, CustomerContext.Schema))
            .ConfigureWarnings(w => w.Ignore(SqlServerEventId.SavepointsDisabledBecauseOfMARS)));
    }

    public const string InvalidLastname = "Exception";
    private readonly string[] _surnames = ["Doe", "Smith", InvalidLastname];


    [Theory]
    [InlineData([TransactionType.SqlTransaction, BusType.AzureSB, 100, SqlOutboxMessageIdGenerationMode.ClientGuidGenerator])]
    [InlineData([TransactionType.SqlTransaction, BusType.AzureSB, 100, SqlOutboxMessageIdGenerationMode.DatabaseGeneratedSequentialGuid])]
    [InlineData([TransactionType.SqlTransaction, BusType.AzureSB, 100, SqlOutboxMessageIdGenerationMode.DatabaseGeneratedGuid])]
    [InlineData([TransactionType.TransactionScope, BusType.AzureSB, 100, SqlOutboxMessageIdGenerationMode.ClientGuidGenerator])]
    [InlineData([TransactionType.SqlTransaction, BusType.Kafka, 100, SqlOutboxMessageIdGenerationMode.ClientGuidGenerator])]
    public async Task Given_CommandHandlerInTransaction_When_ExceptionThrownDuringHandlingRaisedAtTheEnd_Then_TransactionIsRolledBack_And_NoDataSaved_And_NoEventRaised(TransactionType transactionType, BusType busType, int messageCount, SqlOutboxMessageIdGenerationMode mode)
    {
        // arrange
        _testParamUseHybridBus = true;
        _testParamTransactionType = transactionType;
        _testParamBusType = busType;
        _testParamIdGenerationMode = mode;

        await PrepareDatabase();

        var commands = Enumerable.Range(0, messageCount).Select(x => new CreateCustomerCommand($"John {x:000}", _surnames[x % _surnames.Length])).ToList();
        var validCommands = commands.Where(x => !string.Equals(x.Lastname, InvalidLastname, StringComparison.InvariantCulture)).ToList();

        await EnsureConsumersStarted();

        var store = ServiceProvider!.GetRequiredService<TestEventCollector<CustomerCreatedEvent>>();
        // skip any outstanding messages from previous run
        await store.WaitUntilArriving(newMessagesTimeout: 5);
        store.Clear();

        // act
        var sendTasks = commands
            .Select(async cmd =>
            {
                var unitOfWorkScope = ServiceProvider!.CreateScope();
                await using (unitOfWorkScope as IAsyncDisposable)
                {
                    var bus = unitOfWorkScope.ServiceProvider.GetRequiredService<IMessageBus>();

                    try
                    {
                        var res = await bus.Send(cmd);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogInformation("Exception occurred while handling cmd {Command}: {Message}", cmd, ex.Message);
                    }
                }
            })
            .ToArray();

        await Task.WhenAll(sendTasks);

        // Wait for messages to arrive first (they should start arriving quickly)
        await store.WaitUntilArriving(newMessagesTimeout: 20);
        
        Logger.LogInformation("Received {ActualCount}/{ExpectedCount} messages after initial wait", store.Count, validCommands.Count);

        // assert
        using var scope = ServiceProvider!.CreateScope();

        // Ensure the expected number of events was actually published to ASB and delivered via that channel.
        store.Count.Should().Be(validCommands.Count);

        var customerContext = scope.ServiceProvider!.GetRequiredService<CustomerContext>();

        // Ensure the DB also has the expected record
        var customerCountWithInvalidLastname = await customerContext.Customers.CountAsync(x => x.Lastname == InvalidLastname);
        var customerCountWithValidLastname = await customerContext.Customers.CountAsync(x => x.Lastname != InvalidLastname);

        customerCountWithInvalidLastname.Should().Be(0);
        customerCountWithValidLastname.Should().Be(validCommands.Count);
    }

    private Task PrepareDatabase()
        => PerformDbOperation(async (context, _) =>
        {
            // migrate db
            await context.Database.MigrateAsync();
            await context.Customers.ExecuteDeleteAsync();
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
            await context.Database.ExecuteSqlRawAsync($"""
                if exists (select 1
                           from INFORMATION_SCHEMA.TABLES
                           where TABLE_SCHEMA = '{context.Model.GetDefaultSchema()}'
                             and TABLE_NAME = 'Outbox')
                    truncate table {context.Model.GetDefaultSchema()}.Outbox;
                """);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.
        });

    [Theory]
    [InlineData([BusType.AzureSB, 100, SqlOutboxMessageIdGenerationMode.ClientGuidGenerator])]
    [InlineData([BusType.AzureSB, 100, SqlOutboxMessageIdGenerationMode.DatabaseGeneratedGuid])]
    [InlineData([BusType.AzureSB, 100, SqlOutboxMessageIdGenerationMode.DatabaseGeneratedSequentialGuid])]
    [InlineData([BusType.Kafka, 100, SqlOutboxMessageIdGenerationMode.ClientGuidGenerator])]
    public async Task Given_PublishExternalEventInTransaction_When_ExceptionThrownDuringHandlingRaisedAtTheEnd_Then_TransactionIsRolledBack_And_NoEventRaised(BusType busType, int messageCount, SqlOutboxMessageIdGenerationMode mode)
    {
        // arrange
        _testParamUseHybridBus = false;
        _testParamTransactionType = TransactionType.TransactionScope;
        _testParamBusType = busType;
        _testParamIdGenerationMode = mode;

        await PrepareDatabase();

        var surnames = new[] { "Doe", "Smith", InvalidLastname };
        var events = Enumerable.Range(0, messageCount).Select(x => new CustomerCreatedEvent(Guid.NewGuid(), $"John {x:000}", surnames[x % surnames.Length])).ToList();
        var validEvents = events.Where(x => !string.Equals(x.Lastname, InvalidLastname, StringComparison.InvariantCulture)).ToList();

        await EnsureConsumersStarted();

        var store = ServiceProvider!.GetRequiredService<TestEventCollector<CustomerCreatedEvent>>();
        // skip any outstanding messages from previous run
        await store.WaitUntilArriving(newMessagesTimeout: 5);
        store.Clear();

        // act
        var publishTasks = events
            .Select(async ev =>
            {
                var unitOfWorkScope = ServiceProvider!.CreateScope();
                await using (unitOfWorkScope as IAsyncDisposable)
                {
                    var bus = unitOfWorkScope.ServiceProvider.GetRequiredService<IMessageBus>();
                    var txService = unitOfWorkScope.ServiceProvider.GetRequiredService<ISqlTransactionService>();

                    await txService.BeginTransaction();
                    try
                    {
                        await bus.Publish(ev, headers: new Dictionary<string, object> { ["CustomerId"] = ev.Id });

                        if (ev.Lastname == InvalidLastname)
                        {
                            // This will simulate an exception that happens close message handling completion, which should roll back the db transaction.
                            throw new ApplicationException("Invalid last name");
                        }

                        await txService.CommitTransaction();
                    }
                    catch (Exception ex)
                    {
                        await txService.RollbackTransaction();
                        Logger.LogInformation("Exception occurred while handling cmd {Command}: {Message}", ev, ex.Message);
                    }
                }
            })
            .ToArray();

        await Task.WhenAll(publishTasks);

        // Wait for messages to arrive (they should start arriving quickly)
        await store.WaitUntilArriving(newMessagesTimeout: 20);
        
        Logger.LogInformation("Received {ActualCount}/{ExpectedCount} messages after initial wait", store.Count, validEvents.Count);

        // assert

        // Ensure the expected number of events was actually published to ASB and delivered via that channel.
        store.Count.Should().Be(validEvents.Count);
    }
}

public record CreateCustomerCommand(string Firstname, string Lastname) : IRequest<Guid>;

public class CreateCustomerCommandHandler(IMessageBus Bus, CustomerContext CustomerContext) : IRequestHandler<CreateCustomerCommand, Guid>
{
    public async Task<Guid> OnHandle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        // Note: This handler will be already wrapped in a transaction: see Program.cs and .UseTransactionScope() / .UseSqlTransaction() 

        var uniqueId = await Bus.Send(new GenerateCustomerIdCommand(request.Firstname, request.Lastname), cancellationToken: cancellationToken);

        var customer = new Customer(request.Firstname, request.Lastname, uniqueId);
        await CustomerContext.Customers.AddAsync(customer, cancellationToken);
        await CustomerContext.SaveChangesAsync(cancellationToken);

        // Announce to anyone outside of this micro-service that a customer has been created (this will go out via an transactional outbox)
        await Bus.Publish(new CustomerCreatedEvent(customer.Id, customer.Firstname, customer.Lastname), headers: new Dictionary<string, object> { ["CustomerId"] = customer.Id }, cancellationToken: cancellationToken);

        // Simulate some variable processing time
        await Task.Delay(Random.Shared.Next(10, 250), cancellationToken);

        if (request.Lastname == OutboxTests.InvalidLastname)
        {
            // This will simulate an exception that happens close message handling completion, which should roll back the db transaction.
            throw new ApplicationException("Invalid last name");
        }

        return customer.Id;
    }
}

public record GenerateCustomerIdCommand(string Firstname, string Lastname) : IRequest<string>;

public class GenerateCustomerIdCommandHandler : IRequestHandler<GenerateCustomerIdCommand, string>
{
    public Task<string> OnHandle(GenerateCustomerIdCommand request, CancellationToken cancellationToken)
    {
        // Note: This handler will be already wrapped in a transaction: see Program.cs and .UseTransactionScope() / .UseSqlTransaction() 

        if (request.Lastname == OutboxTests.InvalidLastname)
        {
            throw new ApplicationException("Invalid last name");
        }

        // generate a dummy customer id
        return Task.FromResult($"{request.Firstname.ToUpperInvariant()[..3]}-{request.Lastname.ToUpperInvariant()[..3]}-{Guid.NewGuid()}");
    }
}

public record CustomerCreatedEvent(Guid Id, string Firstname, string Lastname);

public class CustomerCreatedEventConsumer(TestEventCollector<CustomerCreatedEvent> Store) : IConsumer<CustomerCreatedEvent>, IConsumerWithContext
{
    public IConsumerContext Context { get; set; }

    public Task OnHandle(CustomerCreatedEvent message, CancellationToken cancellationToken)
    {
        if (Context != null && Context.Headers.TryGetValue("CustomerId", out var customerId))
        {
            if (customerId != null && message.Id == Guid.Parse(customerId.ToString()))
            {
                Store.Add(message);
            }
        }
        return Task.CompletedTask;
    }
}
