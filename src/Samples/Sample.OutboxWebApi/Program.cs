using System.Reflection;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Sample.OutboxWebApi;
using Sample.OutboxWebApi.Application;
using Sample.OutboxWebApi.DataAccess;

using SecretStore;

using SlimMessageBus.Host;
using SlimMessageBus.Host.AzureServiceBus;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Outbox;
using SlimMessageBus.Host.Outbox.PostgreSql;
using SlimMessageBus.Host.Outbox.Sql;
using SlimMessageBus.Host.Serialization.Json;

// Local file with secrets
Secrets.Load(@"..\..\secrets.txt");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();

var configuration = builder.Configuration;

var dbProvider = DbProvider.PostgreSql;

// doc:fragment:ExampleStartup
builder.Services.AddSlimMessageBus(mbb =>
{
    mbb.PerMessageScopeEnabled(false);
    mbb
        .AddChildBus("Memory", mbb =>
        {
            mbb.WithProviderMemory()
                .AutoDeclareFrom(Assembly.GetExecutingAssembly(), consumerTypeFilter: t => t.Name.EndsWith("CommandHandler"));
            //.UseTransactionScope(messageTypeFilter: t => t.Name.EndsWith("Command")) // Consumers/Handlers will be wrapped in a TransactionScope
            //.UseSqlTransaction(messageTypeFilter: t => t.Name.EndsWith("Command")); // Consumers/Handlers will be wrapped in a SqlTransaction ending with Command

            switch (dbProvider)
            {
                case DbProvider.SqlServer:
                    mbb.UseSqlTransaction(messageTypeFilter: t => t.Name.EndsWith("Command")); // Consumers/Handlers will be wrapped in a SqlTransaction ending with Command
                    break;

                case DbProvider.PostgreSql:
                    mbb.UsePostgreSqlTransaction(messageTypeFilter: t => t.Name.EndsWith("Command")); // Consumers/Handlers will be wrapped in a SqlTransaction ending with Command
                    break;
            }
        })
        .AddChildBus("AzureSB", mbb =>
        {
            mbb
                .Handle<CreateCustomerCommand, Guid>(s =>
                {
                    s.Topic("samples.outbox/customer-events", t =>
                    {
                        t.WithHandler<CreateCustomerCommandHandler, CreateCustomerCommand>()
                            .SubscriptionName("CreateCustomer");
                    });
                })
                .WithProviderServiceBus(cfg =>
                {
                    cfg.ConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);
                    cfg.TopologyProvisioning.CanProducerCreateTopic = true;
                    cfg.TopologyProvisioning.CanConsumerCreateQueue = true;
                    cfg.TopologyProvisioning.CanConsumerReplaceSubscriptionFilters = true;
                })
                .Produce<CustomerCreatedEvent>(x =>
                {
                    x.DefaultTopic("samples.outbox/customer-events");
                    // OR if you want just this producer to sent via outbox
                    // x.UseOutbox();
                })
                // All outgoing messages from this bus will go out via an outbox
                .UseOutbox(/* messageTypeFilter: t => t.Name.EndsWith("Command") */); // Additionally, can apply filter do determine messages that should go out via outbox                
        })
        .AddServicesFromAssembly(Assembly.GetExecutingAssembly())
        .AddJsonSerializer()
        .AddAspNet();

    switch (dbProvider)
    {
        case DbProvider.SqlServer:
            SlimMessageBus.Host.Outbox.Sql.DbContext.MessageBusBuilderExtensions.AddOutboxUsingDbContext<CustomerContext>(mbb, opts =>
            {
                opts.PollBatchSize = 500;
                opts.PollIdleSleep = TimeSpan.FromSeconds(10);
                opts.MessageCleanup.Interval = TimeSpan.FromSeconds(10);
                opts.MessageCleanup.Age = TimeSpan.FromMinutes(1);
                //opts.SqlSettings.TransactionIsolationLevel = System.Data.IsolationLevel.RepeatableRead;
                //opts.SqlSettings.Dialect = SqlDialect.SqlServer;
            });

            break;

        case DbProvider.PostgreSql:
            SlimMessageBus.Host.Outbox.PostgreSql.DbContext.MessageBusBuilderExtensions.AddOutboxUsingDbContext<CustomerContext>(mbb, opts =>
            {
                opts.PollBatchSize = 500;
                opts.PollIdleSleep = TimeSpan.FromSeconds(10);
                opts.MessageCleanup.Interval = TimeSpan.FromSeconds(10);
                opts.MessageCleanup.Age = TimeSpan.FromMinutes(1);
                //opts.SqlSettings.TransactionIsolationLevel = System.Data.IsolationLevel.RepeatableRead;
                //opts.SqlSettings.Dialect = SqlDialect.SqlServer;
            });

            break;
    }
});
// doc:fragment:ExampleStartup

/*
// Alternatively, if we were not using EF, we could use a SqlConnection
builder.Services.AddSlimMessageBusOutboxUsingSql(opts => { opts.PollBatchSize = 100; });

// Register in the container how to create SqlConnection
builder.Services.AddTransient(svp =>
    var configuration = svp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("SqlServerConnection");
    return new SqlConnection(connectionString);
});
*/

// Entity Framework setup - application specific EF DbContext
switch (dbProvider)
{
    case DbProvider.SqlServer:
        builder.Services.AddDbContext<CustomerContext>(
            options => options.UseSqlServer(Secrets.Service.PopulateSecrets(builder.Configuration.GetConnectionString("SqlServerConnection")),
                b => b.MigrationsAssembly("Sample.OutboxWebApi.SqlServer")));
        break;

    case DbProvider.PostgreSql:
        builder.Services.AddDbContext<CustomerContext>(
            options => options.UseNpgsql(Secrets.Service.PopulateSecrets(builder.Configuration.GetConnectionString("PostgreSqlConnection")),
                b => b.MigrationsAssembly("Sample.OutboxWebApi.Postgres")));
        break;
}

var app = builder.Build();

async Task CreateDbIfNotExists()
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<CustomerContext>();

        // Note: if the db is not being created, ensure that __EFMigrationsHistory does not exist
        await context.Database.EnsureCreatedAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating the DB.");
    }

    // warm up the bus and force the singleton creation
    _ = services.GetRequiredService<IMessageBus>();
}

await CreateDbIfNotExists();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/customer", ([FromBody] CreateCustomerCommand request, IMessageBus bus) => bus.Send(request))
   .WithName("CreateCustomer")
   .WithOpenApi();

await app.RunAsync();

