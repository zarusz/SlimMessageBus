namespace SlimMessageBus.Host.Outbox.DbContext.Test;

using System.Reflection;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SecretStore;

using SlimMessageBus.Host.AzureServiceBus;
using SlimMessageBus.Host.Hybrid;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.MsDependencyInjection;
using SlimMessageBus.Host.Outbox.DbContext.Test.DataAccess;
using SlimMessageBus.Host.Outbox.Sql;
using SlimMessageBus.Host.Serialization.SystemTextJson;
using SlimMessageBus.Host.Test.Common;

using Xunit.Abstractions;

[Trait("Category", "Integration")]
public class OutboxTests : BaseIntegrationTest<OutboxTests>
{
    private TransactionType _testParamTransactionType;

    public OutboxTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    public enum TransactionType
    {
        SqlTransaction,
        TrnasactionScope
    }

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus((mbb, svp) =>
        {
            var cfg = svp.GetRequiredService<IConfiguration>();

            mbb
                .WithProviderHybrid()
                .WithSerializer(new JsonMessageSerializer())
                .AddChildBus("Memory", mbb =>
                {
                    mbb.WithProviderMemory()
                       .AutoDeclareFrom(Assembly.GetExecutingAssembly(), consumerTypeFilter: t => t.Name.Contains("Command"));

                    if (_testParamTransactionType == TransactionType.SqlTransaction)
                    {
                        mbb.UseSqlTransaction(); // Consumers/Handlers will be wrapped in a SqlTransaction
                    }
                    if (_testParamTransactionType == TransactionType.TrnasactionScope)
                    {
                        mbb.UseTransactionScope(); // Consumers/Handlers will be wrapped in a TransactionScope
                    }
                })
                .AddChildBus("AzureSB", mbb =>
                {
                    var serviceBusConnectionString = Secrets.Service.PopulateSecrets(cfg["Azure:ServiceBus"]);
                    var topic = "tests.outbox/customer-events";
                    mbb.WithProviderServiceBus(new ServiceBusMessageBusSettings(serviceBusConnectionString))
                       .Produce<CustomerCreatedEvent>(x => x.DefaultTopic(topic))
                       .Consume<CustomerCreatedEvent>(x => x.Topic(topic).SubscriptionName(nameof(OutboxTests)).WithConsumer<CustomerCreatedEventConsumer>())
                       .UseOutbox(); // All outgoing messages from this bus will go out via an outbox
                });
        }, addConsumersFromAssembly: new[] { Assembly.GetExecutingAssembly() });

        services.AddMessageBusOutboxUsingDbContext<CustomerContext>(opts =>
        {
            opts.PollBatchSize = 100;
            opts.PollIdleSleep = TimeSpan.FromSeconds(0.5);
            opts.MessageCleanup.Interval = TimeSpan.FromSeconds(10);
            opts.MessageCleanup.Age = TimeSpan.FromMinutes(1);
            opts.DatabaseTableName = "IntTest_Outbox";
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
    [InlineData(new object[] { TransactionType.SqlTransaction })]
    [InlineData(new object[] { TransactionType.TrnasactionScope })]
    public async Task Given_CommandHandlerInTransaction_When_ExceptionThrownDuringHandlingRaisedAtTheEnd_Then_TransactionIsRolledBack_And_NoDataSaved_And_NoEventRaised(TransactionType transactionType)
    {
        // arrange
        _testParamTransactionType = transactionType;

        await PerformDbOperation(async context =>
        {
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

        // act
        foreach (var cmd in commands)
        {
            using var unitOfWorkScope = ServiceProvider!.CreateScope();

            var bus = unitOfWorkScope.ServiceProvider.GetRequiredService<IMessageBus>();

            try
            {
                var res = await bus.Send(cmd);
            }
            catch
            {
            }
        }

        await store.WaitUntilArriving(newMessagesTimeout: 5, expectedCount: validCommands.Count);

        // assert
        using var scope = ServiceProvider!.CreateScope();

        var customerContext = scope.ServiceProvider!.GetRequiredService<CustomerContext>();

        // Ensure the expected number of events was actually published to ASB and delivered via that channel.
        validCommands.Count.Should().Be(store.Count);

        // Ensure the DB also has the expected record
        var customerCountWithInvalidLastname = await customerContext.Customers.CountAsync(x => x.Lastname == InvalidLastname);
        var customerCountWithValidLastname = await customerContext.Customers.CountAsync(x => x.Lastname != InvalidLastname);

        customerCountWithInvalidLastname.Should().Be(0);
        customerCountWithValidLastname.Should().Be(validCommands.Count);
    }
}

public record CreateCustomerCommand(string Firstname, string Lastname) : IRequestMessage<Guid>;

public record CreateCustomerCommandHandler(IMessageBus Bus, CustomerContext CustomerContext) : IRequestHandler<CreateCustomerCommand, Guid>
{
    public async Task<Guid> OnHandle(CreateCustomerCommand request)
    {
        // Note: This handler will be already wrapped in a transaction: see Program.cs and .UseTransactionScope() / .UseSqlTransaction() 

        var customer = new Customer(request.Firstname, request.Lastname);
        await CustomerContext.Customers.AddAsync(customer);
        await CustomerContext.SaveChangesAsync();

        // Announce to anyone outside of this micro-service that a customer has been created (this will go out via an transactional outbox)
        await Bus.Publish(new CustomerCreatedEvent(customer.Id, customer.Firstname, customer.Lastname));

        // Simulate some variable processing time
        await Task.Delay(Random.Shared.Next(10, 500));

        if (request.Lastname == OutboxTests.InvalidLastname)
        {
            // This will simulate an exception that happens close message handling completion, which should roll back the db transaction.
            throw new ApplicationException("Invalid last name");
        }

        return customer.Id;
    }
}

public record CustomerCreatedEvent(Guid Id, string Firstname, string Lastname);

public record CustomerCreatedEventConsumer(TestEventCollector<CustomerCreatedEvent> Store) : IConsumer<CustomerCreatedEvent>
{
    public Task OnHandle(CustomerCreatedEvent message)
    {
        Store.Add(message);
        return Task.CompletedTask;
    }
}
