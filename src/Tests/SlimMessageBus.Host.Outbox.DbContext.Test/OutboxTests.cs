﻿namespace SlimMessageBus.Host.Outbox.DbContext.Test;

using Microsoft.EntityFrameworkCore.Migrations;

using SlimMessageBus.Host.Sql.Common;

[Trait("Category", "Integration")]
[Trait("Transport", "Outbox")]
[Collection(CustomerContext.Schema)]
public class OutboxTests(ITestOutputHelper testOutputHelper) : BaseOutboxIntegrationTest<OutboxTests>(testOutputHelper)
{
    private bool _testParamUseHybridBus;
    private TransactionType _testParamTransactionType;
    private BusType _testParamBusType;

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        static void AddKafkaSsl(string username, string password, ClientConfig c)
        {
            // cloudkarafka.com uses SSL with SASL authentication
            c.SecurityProtocol = SecurityProtocol.SaslSsl;
            c.SaslUsername = username;
            c.SaslPassword = password;
            c.SaslMechanism = SaslMechanism.ScramSha256;
            c.SslCaLocation = "cloudkarafka_2023-10.pem";
        }

        void ConfigureExternalBus(MessageBusBuilder mbb)
        {
            var topic = "test-ping";
            if (_testParamBusType == BusType.Kafka)
            {
                var kafkaBrokers = Secrets.Service.PopulateSecrets(configuration["Kafka:Brokers"]);
                var kafkaUsername = Secrets.Service.PopulateSecrets(configuration["Kafka:Username"]);
                var kafkaPassword = Secrets.Service.PopulateSecrets(configuration["Kafka:Password"]);
                var kafkaSecure = bool.TryParse(Secrets.Service.PopulateSecrets(configuration["Kafka:Secure"]), out var secure) && secure;

                mbb.WithProviderKafka(cfg =>
                {
                    cfg.BrokerList = kafkaBrokers;
                    cfg.ProducerConfig = (config) =>
                    {
                        config.LingerMs = 5; // 5ms
                        config.SocketNagleDisable = true;

                        if (kafkaSecure)
                        {
                            AddKafkaSsl(kafkaUsername, kafkaPassword, config);
                        }
                    };
                    cfg.ConsumerConfig = (config) =>
                    {
                        config.FetchErrorBackoffMs = 1;
                        config.SocketNagleDisable = true;
                        // when the test containers start there is no consumer group yet, so we want to start from the beginning
                        config.AutoOffsetReset = AutoOffsetReset.Earliest;

                        if (kafkaSecure)
                        {
                            AddKafkaSsl(kafkaUsername, kafkaPassword, config);
                        }
                    };
                });
                // Topics on cloudkarafka.com are prefixed with username
                topic = $"{kafkaUsername}-test-ping";
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
                opts.PollIdleSleep = TimeSpan.FromSeconds(1);
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

    public const string InvalidLastname = "Exception";

    [Theory]
    [InlineData([TransactionType.SqlTransaction, BusType.AzureSB, 100])]
    [InlineData([TransactionType.TransactionScope, BusType.AzureSB, 100])]
    [InlineData([TransactionType.SqlTransaction, BusType.Kafka, 100])]
    public async Task Given_CommandHandlerInTransaction_When_ExceptionThrownDuringHandlingRaisedAtTheEnd_Then_TransactionIsRolledBack_And_NoDataSaved_And_NoEventRaised(TransactionType transactionType, BusType busType, int messageCount)
    {
        // arrange
        _testParamUseHybridBus = true;
        _testParamTransactionType = transactionType;
        _testParamBusType = busType;

        await PerformDbOperation(async (context, _) =>
        {
            // migrate db
            await context.Database.DropSchemaIfExistsAsync(context.Model.GetDefaultSchema());
            await context.Database.MigrateAsync();
        });

        var surnames = new[] { "Doe", "Smith", InvalidLastname };
        var commands = Enumerable.Range(0, messageCount).Select(x => new CreateCustomerCommand($"John {x:000}", surnames[x % surnames.Length]));
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

        await store.WaitUntilArriving(newMessagesTimeout: 5, expectedCount: validCommands.Count);

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

    [Theory]
    [InlineData([BusType.AzureSB, 100])]
    [InlineData([BusType.Kafka, 100])]
    public async Task Given_PublishExternalEventInTransaction_When_ExceptionThrownDuringHandlingRaisedAtTheEnd_Then_TransactionIsRolledBack_And_NoEventRaised(BusType busType, int messageCount)
    {
        // arrange
        _testParamUseHybridBus = false;
        _testParamTransactionType = TransactionType.TransactionScope;
        _testParamBusType = busType;

        await PerformDbOperation(async (context, _) =>
        {
            // migrate db
            await context.Database.DropSchemaIfExistsAsync(context.Model.GetDefaultSchema());
            await context.Database.MigrateAsync();
        });

        var surnames = new[] { "Doe", "Smith", InvalidLastname };
        var events = Enumerable.Range(0, messageCount).Select(x => new CustomerCreatedEvent(Guid.NewGuid(), $"John {x:000}", surnames[x % surnames.Length]));
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

        await store.WaitUntilArriving(newMessagesTimeout: 5, expectedCount: validEvents.Count);

        // assert

        // Ensure the expected number of events was actually published to ASB and delivered via that channel.
        store.Count.Should().Be(validEvents.Count);
    }
}

public record CreateCustomerCommand(string Firstname, string Lastname) : IRequest<Guid>;

public class CreateCustomerCommandHandler(IMessageBus Bus, CustomerContext CustomerContext) : IRequestHandler<CreateCustomerCommand, Guid>
{
    public async Task<Guid> OnHandle(CreateCustomerCommand request)
    {
        // Note: This handler will be already wrapped in a transaction: see Program.cs and .UseTransactionScope() / .UseSqlTransaction() 

        var uniqueId = await Bus.Send(new GenerateCustomerIdCommand(request.Firstname, request.Lastname));

        var customer = new Customer(request.Firstname, request.Lastname, uniqueId);
        await CustomerContext.Customers.AddAsync(customer);
        await CustomerContext.SaveChangesAsync();

        // Announce to anyone outside of this micro-service that a customer has been created (this will go out via an transactional outbox)
        await Bus.Publish(new CustomerCreatedEvent(customer.Id, customer.Firstname, customer.Lastname), headers: new Dictionary<string, object> { ["CustomerId"] = customer.Id });

        // Simulate some variable processing time
        await Task.Delay(Random.Shared.Next(10, 250));

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
    public Task<string> OnHandle(GenerateCustomerIdCommand request)
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

    public Task OnHandle(CustomerCreatedEvent message)
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
