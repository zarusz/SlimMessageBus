using System.Reflection;

using SlimMessageBus.Host.AspNetCore;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.AzureServiceBus;
using SlimMessageBus.Host.Hybrid;
using SlimMessageBus.Host.Outbox;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus;
using SecretStore;
using Microsoft.AspNetCore.Mvc;
using Sample.OutboxWebApi.DataAccess;
using Microsoft.EntityFrameworkCore;
using Sample.OutboxWebApi.Application;
using SlimMessageBus.Host.Outbox.DbContext;
using SlimMessageBus.Host.Outbox.Sql;

// Local file with secrets
Secrets.Load(@"..\..\secrets.txt");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();

builder.Services.AddSlimMessageBus((mbb, svp) =>
{
    var cfg = svp.GetRequiredService<IConfiguration>();

    mbb
        .AddChildBus("Memory", mbb =>
        {
            mbb.WithProviderMemory()
               .AutoDeclareFrom(Assembly.GetExecutingAssembly(), consumerTypeFilter: t => t.Name.Contains("Command"))
               //.UseTransactionScope(); // Consumers/Handlers will be wrapped in a TransactionScope
               .UseSqlTransaction(); // Consumers/Handlers will be wrapped in a SqlTransaction
        })
        .AddChildBus("AzureSB", mbb =>
        {
            var serviceBusConnectionString = Secrets.Service.PopulateSecrets(cfg["Azure:ServiceBus"]);
            mbb.WithProviderServiceBus(new ServiceBusMessageBusSettings(serviceBusConnectionString))
               .Produce<CustomerCreatedEvent>(x =>
               {
                   x.DefaultTopic("samples.outbox/customer-events");
                   // OR if you want just this producer to sent via outbox
                   // x.UseOutbox();
               })
               .UseOutbox(); // All outgoing messages from this bus will go out via an outbox
        })
        .WithSerializer(new JsonMessageSerializer())
        .WithProviderHybrid();
}, addConsumersFromAssembly: new[] { Assembly.GetExecutingAssembly() });

builder.Services.AddMessageBusOutboxUsingDbContext<CustomerContext>(opts =>
{
    opts.PollBatchSize = 100;
    opts.MessageCleanup.Interval = TimeSpan.FromSeconds(10);
    opts.MessageCleanup.Age = TimeSpan.FromMinutes(1);
    //opts.TransactionIsolationLevel = System.Data.IsolationLevel.RepeatableRead;
    //opts.Dialect = SqlDialect.SqlServer;
});

/*
// Alternatively, if we were not using EF, we could use a SqlConnection
builder.Services.AddSlimMessageBusOutboxUsingSql(opts => { opts.PollBatchSize = 100; });

// Register in the the container how to create SqlConnection
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

