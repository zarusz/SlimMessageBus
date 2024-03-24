namespace SlimMessageBus.Host.Outbox.DbContext.Test;

using System.Reflection;

using Confluent.Kafka;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SecretStore;

using SlimMessageBus.Host;
using SlimMessageBus.Host.AzureServiceBus;
using SlimMessageBus.Host.Kafka;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Outbox.DbContext.Test.DataAccess;
using SlimMessageBus.Host.Outbox.Sql;
using SlimMessageBus.Host.Serialization.SystemTextJson;
using SlimMessageBus.Host.Test.Common.IntegrationTest;

[Trait("Category", "Integration")]
public class OutboxTests(ITestOutputHelper testOutputHelper) : BaseIntegrationTest<OutboxTests>(testOutputHelper)
{
    private TransactionType _testParamTransactionType;
    private BusType _testParamBusType;

    public enum TransactionType
    {
        SqlTransaction,
        TarnsactionScope
    }

    public enum BusType
    {
        AzureSB,
        Kafka,
    }

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus(mbb =>
        {
            mbb.AddChildBus("Memory", mbb =>
            {
                mbb.WithProviderMemory()
                    .AutoDeclareFrom(Assembly.GetExecutingAssembly(), consumerTypeFilter: t => t.Name.Contains("Command"));

                if (_testParamTransactionType == TransactionType.SqlTransaction)
                {
                    mbb.UseSqlTransaction(); // Consumers/Handlers will be wrapped in a SqlTransaction
                }
                if (_testParamTransactionType == TransactionType.TarnsactionScope)
                {
                    mbb.UseTransactionScope(); // Consumers/Handlers will be wrapped in a TransactionScope
                }
            });
            mbb.AddChildBus("ExternalBus", mbb =>
            {
                var topic = "";
                if (_testParamBusType == BusType.Kafka)
                {
                    var kafkaBrokers = configuration["Kafka:Brokers"];
                    var kafkaUsername = Secrets.Service.PopulateSecrets(configuration["Kafka:Username"]);
                    var kafkaPassword = Secrets.Service.PopulateSecrets(configuration["Kafka:Password"]);

                    mbb.WithProviderKafka(cfg =>
                    {
                        cfg.BrokerList = kafkaBrokers;
                        cfg.ProducerConfig = (config) =>
                        {
                            AddKafkaSsl(kafkaUsername, kafkaPassword, config);

                            config.LingerMs = 5; // 5ms
                            config.SocketNagleDisable = true;
                        };
                        cfg.ConsumerConfig = (config) =>
                        {
                            AddKafkaSsl(kafkaUsername, kafkaPassword, config);

                            config.FetchErrorBackoffMs = 1;
                            config.SocketNagleDisable = true;

                            config.StatisticsIntervalMs = 500000;
                            config.AutoOffsetReset = AutoOffsetReset.Latest;
                        };
                    });

                    topic = $"{kafkaUsername}-test-ping";
                }
                if (_testParamBusType == BusType.AzureSB)
                {
                    mbb.WithProviderServiceBus(cfg => cfg.ConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]));
                    topic = "tests.outbox/customer-events";
                }

                mbb
                    .Produce<CustomerCreatedEvent>(x => x.DefaultTopic(topic))
                    .Consume<CustomerCreatedEvent>(x => x
                        .Topic(topic)
                        .WithConsumer<CustomerCreatedEventConsumer>()
                        .SubscriptionName(nameof(OutboxTests)) // for AzureSB
                        .KafkaGroup("subscriber")) // for Kafka
                    .UseOutbox(); // All outgoing messages from this bus will go out via an outbox
            });
            mbb.AddServicesFromAssembly(Assembly.GetExecutingAssembly());
            mbb.AddJsonSerializer();
            mbb.AddOutboxUsingDbContext<CustomerContext>(opts =>
            {
                opts.PollBatchSize = 100;
                opts.PollIdleSleep = TimeSpan.FromSeconds(0.5);
                opts.MessageCleanup.Interval = TimeSpan.FromSeconds(10);
                opts.MessageCleanup.Age = TimeSpan.FromMinutes(1);
                opts.SqlSettings.DatabaseTableName = "IntTest_Outbox";
            });
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

    public const string InvalidLastname = "Exception";

    [Theory]
    [InlineData([TransactionType.SqlTransaction, BusType.AzureSB])]
    [InlineData([TransactionType.TarnsactionScope, BusType.AzureSB])]
    [InlineData([TransactionType.SqlTransaction, BusType.Kafka])]
    public async Task Given_CommandHandlerInTransaction_When_ExceptionThrownDuringHandlingRaisedAtTheEnd_Then_TransactionIsRolledBack_And_NoDataSaved_And_NoEventRaised(TransactionType transactionType, BusType busType)
    {
        // arrange
        _testParamTransactionType = transactionType;
        _testParamBusType = busType;

        await PerformDbOperation(async context =>
        {
            // clean outbox from previous test run
            await context.Database.ExecuteSqlRawAsync("delete from dbo.IntTest_Outbox");

            // migrate db
            await context.Database.MigrateAsync();

            // clean the customers table
            var cust = await context.Customers.ToListAsync();
            context.Customers.RemoveRange(cust);
            await context.SaveChangesAsync();
        });

        var surnames = new[] { "Doe", "Smith", InvalidLastname };
        var commands = Enumerable.Range(0, 100).Select(x => new CreateCustomerCommand($"John {x:000}", surnames[x % surnames.Length]));
        var validCommands = commands.Where(x => !string.Equals(x.Lastname, InvalidLastname, StringComparison.InvariantCulture)).ToList();
        var store = ServiceProvider!.GetRequiredService<TestEventCollector<CustomerCreatedEvent>>();

        await EnsureConsumersStarted();

        // skip any outstanding messages from previous run
        await store.WaitUntilArriving(newMessagesTimeout: 5);
        store.Clear();

        // act
        foreach (var cmd in commands)
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
                    Logger.LogInformation("Exception occured while handling cmd {Command}: {Message}", cmd, ex.Message);
                }
            }
        }

        await store.WaitUntilArriving(newMessagesTimeout: 5, expectedCount: validCommands.Count);

        // assert
        using var scope = ServiceProvider!.CreateScope();

        var customerContext = scope.ServiceProvider!.GetRequiredService<CustomerContext>();

        // Ensure the expected number of events was actually published to ASB and delivered via that channel.
        store.Count.Should().Be(validCommands.Count);

        // Ensure the DB also has the expected record
        var customerCountWithInvalidLastname = await customerContext.Customers.CountAsync(x => x.Lastname == InvalidLastname);
        var customerCountWithValidLastname = await customerContext.Customers.CountAsync(x => x.Lastname != InvalidLastname);

        customerCountWithInvalidLastname.Should().Be(0);
        customerCountWithValidLastname.Should().Be(validCommands.Count);
    }

    private static void AddKafkaSsl(string username, string password, ClientConfig c)
    {
        // cloudkarafka.com uses SSL with SASL authentication
        c.SecurityProtocol = SecurityProtocol.SaslSsl;
        c.SaslUsername = username;
        c.SaslPassword = password;
        c.SaslMechanism = SaslMechanism.ScramSha256;
        c.SslCaLocation = "cloudkarafka_2023-10.pem";
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
    public async Task<string> OnHandle(GenerateCustomerIdCommand request)
    {
        // Note: This handler will be already wrapped in a transaction: see Program.cs and .UseTransactionScope() / .UseSqlTransaction() 

        if (request.Lastname == OutboxTests.InvalidLastname)
        {
            throw new ApplicationException("Invalid last name");
        }

        // generate a dummy customer id
        return $"{request.Firstname.ToUpperInvariant()[..3]}-{request.Lastname.ToUpperInvariant()[..3]}-{Guid.NewGuid()}";
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
