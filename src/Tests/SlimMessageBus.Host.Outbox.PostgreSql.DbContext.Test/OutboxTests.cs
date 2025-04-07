namespace SlimMessageBus.Host.Outbox.PostgreSql.DbContext.Test;

using Microsoft.Extensions.Logging;

[CollectionDefinition(nameof(OutboxTestsCollection))]
public class OutboxTestsCollection : ICollectionFixture<PostgreSqlFixture>, ICollectionFixture<KafkaFixture>
{
}

[Trait("Category", "Integration")]
[Trait("Transport", "Outbox")]
[Collection(nameof(OutboxTestsCollection))]
public class OutboxTests(ITestOutputHelper output, PostgreSqlFixture postgreSqlFixture, KafkaFixture kafkaFixture) : BaseIntegrationTest<OutboxBenchmarkTests>(output)
{
    public const string InvalidLastName = "Exception";

    private readonly string[] _surnames = ["Doe", "Smith", InvalidLastName];
    private readonly PostgreSqlFixture _postgreSqlFixture = postgreSqlFixture;
    private readonly KafkaFixture _kafkaFixture = kafkaFixture;
    private readonly ITestOutputHelper _output = output;
    private readonly string _databaseName = $"smb_{Guid.NewGuid().ToString("N").ToLowerInvariant().Substring(6)}";

    private bool _testParamUseHybridBus;
    private TransactionType _testParamTransactionType;
    private BusType _testParamBusType;
    private string _topic;

    private async Task PrepareKafkaTopics()
    {
        _topic = $"test-ping-{DateTimeOffset.UtcNow.Ticks}";
        _output.WriteLine($"Creating Kafka topics: {_topic}");
        await _kafkaFixture.CreateTopics(_topic);
    }

    private async Task PrepareDatabase()
    {
        var scope = ServiceProvider!.CreateScope();
        try
        {
            _output.WriteLine($"Creating PostgreSql db: {_databaseName}");
            await using var context = scope.ServiceProvider.GetRequiredService<CustomerContext>();
            await context.Database.EnsureCreatedAsync();
        }
        finally
        {
            await ((IAsyncDisposable)scope).DisposeAsync();
        }
    }

    public override async Task DisposeAsync()
    {
        var scope = ServiceProvider!.CreateScope();
        try
        {
            await using var context = scope.ServiceProvider.GetRequiredService<CustomerContext>();
            await context.Database.EnsureDeletedAsync();
        }
        finally
        {
            await ((IAsyncDisposable)scope).DisposeAsync();
        }

        await base.DisposeAsync();
    }

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        void ConfigureExternalBus(MessageBusBuilder mbb)
        {
            if (_testParamBusType == BusType.Kafka)
            {
                var kafkaBrokers = _kafkaFixture.GetBootstrapAddress();

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
                _topic = $"smb-tests/{nameof(OutboxTests)}/outbox/{DateTimeOffset.UtcNow.Ticks}";
            }

            mbb
                .Produce<CustomerCreatedEvent>(x => x.DefaultTopic(_topic))
                .Consume<CustomerCreatedEvent>(x => x
                    .Topic(_topic)
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

                    if (_testParamTransactionType == TransactionType.DbTransaction)
                    {
                        mbb.UsePostgreSqlTransaction(); // Consumers/Handlers will be wrapped in a SqlTransaction
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
                opts.PollIdleSleep = TimeSpan.FromSeconds(1);
                opts.MessageCleanup.Interval = TimeSpan.FromSeconds(10);
                opts.MessageCleanup.Age = TimeSpan.FromMinutes(1);
            });
        });

        services.AddSingleton<TestEventCollector<CustomerCreatedEvent>>();

        // Entity Framework setup - application specific EF DbContext
        services.AddDbContext<CustomerContext>(
            options => options
                .UseNpgsql(_postgreSqlFixture.GetConnectionString(), x => x.MigrationsHistoryTable(HistoryRepository.DefaultTableName)));
    }

    [Theory]
    [InlineData([TransactionType.DbTransaction, BusType.AzureSB, 100])]
    [InlineData([TransactionType.TransactionScope, BusType.AzureSB, 100])]
    [InlineData([TransactionType.DbTransaction, BusType.Kafka, 100])]
    public async Task Given_CommandHandlerInTransaction_When_ExceptionThrownDuringHandlingRaisedAtTheEnd_Then_TransactionIsRolledBack_And_NoDataSaved_And_NoEventRaised(TransactionType transactionType, BusType busType, int messageCount)
    {
        // arrange
        _testParamUseHybridBus = true;
        _testParamTransactionType = transactionType;
        _testParamBusType = busType;

        var commands = Enumerable.Range(0, messageCount).Select(x => new CreateCustomerCommand($"John {x:000}", _surnames[x % _surnames.Length])).ToList();
        var validCommands = commands.Where(x => !string.Equals(x.LastName, InvalidLastName, StringComparison.InvariantCulture)).ToList();

        if (busType == BusType.Kafka)
        {
            await PrepareKafkaTopics();
        }

        await PrepareDatabase();
        await EnsureConsumersStarted();

        var store = ServiceProvider!.GetRequiredService<TestEventCollector<CustomerCreatedEvent>>();
        // skip any outstanding messages from previous run
        await store.WaitUntilArriving(newMessagesTimeout: 5);
        store.Clear();

        // act
        await Parallel.ForEachAsync(
            commands,
            new ParallelOptions { MaxDegreeOfParallelism = 10 },
            async (cmd, ct) =>
            {
                var unitOfWorkScope = ServiceProvider!.CreateScope();
                await using (unitOfWorkScope as IAsyncDisposable)
                {
                    var bus = unitOfWorkScope.ServiceProvider.GetRequiredService<IMessageBus>();
                    try
                    {
                        _ = await bus.Send(cmd);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogInformation("Exception occurred while handling cmd {Command}: {Message}", cmd, ex.Message);
                    }
                }
            });

        await store.WaitUntilArriving(newMessagesTimeout: 5, expectedCount: validCommands.Count);

        // assert
        using var scope = ServiceProvider!.CreateScope();

        // Ensure the expected number of events was actually published to ASB and delivered via that channel.
        store.Count.Should().Be(validCommands.Count);

        var customerContext = scope.ServiceProvider!.GetRequiredService<CustomerContext>();

        // Ensure the DB also has the expected record
        var customerCountWithInvalidLastName = await customerContext.Customers.CountAsync(x => x.LastName == InvalidLastName);
        var customerCountWithValidLastName = await customerContext.Customers.CountAsync(x => x.LastName != InvalidLastName);

        customerCountWithInvalidLastName.Should().Be(0);
        customerCountWithValidLastName.Should().Be(validCommands.Count);
    }

    [Theory]
    [InlineData([BusType.AzureSB, 100])]
    [InlineData([BusType.Kafka, 100])]
    public async Task Given_PublishExternalEventInTransaction_When_ExceptionThrownDuringHandlingRaisedAtTheEnd_Then_TransactionIsRolledBack_And_NoEventRaised(BusType busType, int messageCount)
    {
        // arrange
        _testParamUseHybridBus = false;
        _testParamTransactionType = TransactionType.TransactionScope;
        _testParamBusType = busType;

        if (busType == BusType.Kafka)
        {
            await PrepareKafkaTopics();
        }

        await PrepareDatabase();

        var surnames = new[] { "Doe", "Smith", InvalidLastName };
        var events = Enumerable.Range(0, messageCount).Select(x => new CustomerCreatedEvent(Guid.NewGuid(), $"John {x:000}", surnames[x % surnames.Length])).ToList();
        var validEvents = events.Where(x => !string.Equals(x.LastName, InvalidLastName, StringComparison.InvariantCulture)).ToList();

        await EnsureConsumersStarted();

        var store = ServiceProvider!.GetRequiredService<TestEventCollector<CustomerCreatedEvent>>();
        // skip any outstanding messages from previous run
        await store.WaitUntilArriving(newMessagesTimeout: 5);
        store.Clear();

        // act
        await Parallel.ForEachAsync(
            events,
            new ParallelOptions { MaxDegreeOfParallelism = 10 },
            async (ev, ct) =>
            {
                var unitOfWorkScope = ServiceProvider!.CreateScope();
                await using (unitOfWorkScope as IAsyncDisposable)
                {
                    var bus = unitOfWorkScope.ServiceProvider.GetRequiredService<IMessageBus>();
                    var txService = unitOfWorkScope.ServiceProvider.GetRequiredService<IPostgreSqlTransactionService>();

                    await txService.BeginTransaction();
                    try
                    {
                        await bus.Publish(ev, headers: new Dictionary<string, object> { ["CustomerId"] = ev.Id });

                        if (ev.LastName == InvalidLastName)
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
            });

        await store.WaitUntilArriving(newMessagesTimeout: 5, expectedCount: validEvents.Count);

        // assert

        // Ensure the expected number of events was actually published to ASB and delivered via that channel.
        store.Count.Should().Be(validEvents.Count);
    }
}

public record CreateCustomerCommand(string FirstName, string LastName) : IRequest<Guid>;

public class CreateCustomerCommandHandler(IMessageBus Bus, CustomerContext CustomerContext) : IRequestHandler<CreateCustomerCommand, Guid>
{
    public async Task<Guid> OnHandle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        // Note: This handler will be already wrapped in a transaction: see Program.cs and .UseTransactionScope() / .UseSqlTransaction() 

        var uniqueId = await Bus.Send(new GenerateCustomerIdCommand(request.FirstName, request.LastName), cancellationToken: cancellationToken);

        var customer = new Customer(request.FirstName, request.LastName, uniqueId);
        await CustomerContext.Customers.AddAsync(customer, cancellationToken);
        await CustomerContext.SaveChangesAsync(cancellationToken);

        // Announce to anyone outside of this micro-service that a customer has been created (this will go out via an transactional outbox)
        await Bus.Publish(new CustomerCreatedEvent(customer.Id, customer.FirstName, customer.LastName), headers: new Dictionary<string, object> { ["CustomerId"] = customer.Id }, cancellationToken: cancellationToken);

        // Simulate some variable processing time
        await Task.Delay(Random.Shared.Next(10, 250), cancellationToken);

        if (request.LastName == OutboxTests.InvalidLastName)
        {
            // This will simulate an exception that happens close message handling completion, which should roll back the db transaction.
            throw new ApplicationException("Invalid last name");
        }

        return customer.Id;
    }
}

public record GenerateCustomerIdCommand(string FirstName, string LastName) : IRequest<string>;

public class GenerateCustomerIdCommandHandler : IRequestHandler<GenerateCustomerIdCommand, string>
{
    public Task<string> OnHandle(GenerateCustomerIdCommand request, CancellationToken cancellationToken)
    {
        // Note: This handler will be already wrapped in a transaction: see Program.cs and .UseTransactionScope() / .UseSqlTransaction() 

        if (request.LastName == OutboxTests.InvalidLastName)
        {
            throw new ApplicationException("Invalid last name");
        }

        // generate a dummy customer id
        return Task.FromResult($"{request.FirstName.ToUpperInvariant()[..3]}-{request.LastName.ToUpperInvariant()[..3]}-{Guid.NewGuid()}");
    }
}

public record CustomerCreatedEvent(Guid Id, string FirstName, string LastName);

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
