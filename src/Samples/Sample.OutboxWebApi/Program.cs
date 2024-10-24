using System.Reflection;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Sample.OutboxWebApi.Application;
using Sample.OutboxWebApi.DataAccess;

using SecretStore;

using SlimMessageBus.Host;
using SlimMessageBus.Host.AzureServiceBus;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Outbox;
using SlimMessageBus.Host.Outbox.Sql;
using SlimMessageBus.Host.Outbox.Sql.DbContext;
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

// doc:fragment:ExampleStartup
builder.Services.AddSlimMessageBus(mbb =>
{
    mbb.PerMessageScopeEnabled(false);
    mbb
        .AddChildBus("Memory", mbb =>
        {
            mbb.WithProviderMemory()
                .AutoDeclareFrom(Assembly.GetExecutingAssembly(), consumerTypeFilter: t => t.Name.EndsWith("CommandHandler"))
                //.UseTransactionScope(messageTypeFilter: t => t.Name.EndsWith("Command")) // Consumers/Handlers will be wrapped in a TransactionScope
                .UseSqlTransaction(messageTypeFilter: t => t.Name.EndsWith("Command")); // Consumers/Handlers will be wrapped in a SqlTransaction ending with Command
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
                .UseOutbox(/* messageTypeFilter: t => t.Name.EndsWith("Command") */); // Additionaly, can apply filter do determine messages that should go out via outbox                
        })
        .AddServicesFromAssembly(Assembly.GetExecutingAssembly())
        .AddJsonSerializer()
        .AddAspNet()
        .AddOutboxUsingDbContext<CustomerContext>(opts =>
        {
            opts.PollBatchSize = 100;
            opts.PollIdleSleep = TimeSpan.FromSeconds(10);
            opts.MessageCleanup.Interval = TimeSpan.FromSeconds(10);
            opts.MessageCleanup.Age = TimeSpan.FromMinutes(1);
            //opts.SqlSettings.TransactionIsolationLevel = System.Data.IsolationLevel.RepeatableRead;
            //opts.SqlSettings.Dialect = SqlDialect.SqlServer;
        });
});
// doc:fragment:ExampleStartup

/*
// Alternatively, if we were not using EF, we could use a SqlConnection
builder.Services.AddSlimMessageBusOutboxUsingSql(opts => { opts.PollBatchSize = 100; });

// Register in the container how to create SqlConnection
builder.Services.AddTransient(svp =>
    var configuration = svp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    return new SqlConnection(connectionString);
});
*/

// Entity Framework setup - application specific EF DbContext
builder.Services.AddDbContext<CustomerContext>(options => options.UseSqlServer(Secrets.Service.PopulateSecrets(builder.Configuration.GetConnectionString("DefaultConnection"))));

var app = builder.Build();

async Task CreateDbIfNotExists()
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<CustomerContext>();
        await context.Database.MigrateAsync();
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

