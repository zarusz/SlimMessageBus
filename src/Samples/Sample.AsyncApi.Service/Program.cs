using System.Reflection;

using Sample.AsyncApi.Service.Messages;

using Saunter;
using Saunter.AsyncApiSchema.v2;

using SecretStore;

using SlimMessageBus.Host;
using SlimMessageBus.Host.AsyncApi;
using SlimMessageBus.Host.AzureServiceBus;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.Json;

// Local file with secrets
Secrets.Load(@"..\..\secrets.txt");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerDocument();

var configuration = builder.Configuration;

builder.Services.AddSlimMessageBus(mbb =>
{
    mbb
        .AddChildBus("Memory", mbb =>
        {
            mbb
                .WithProviderMemory()
                .AutoDeclareFrom(Assembly.GetExecutingAssembly(), consumerTypeFilter: t => t.Name.Contains("Command"));
        })
        .AddChildBus("AzureSB", mbb =>
        {
            mbb
                .WithProviderServiceBus(cfg => cfg.ConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]))
                .Produce<CustomerEvent>(x =>
                {
                    x.DefaultTopic("samples.asyncapi/customer-events");
                })
                .Consume<CustomerEvent>(x =>
                {
                    x.Topic("samples.asyncapi/customer-events");
                    x.SubscriptionName("asyncapi-service");
                    x.WithConsumer<CustomerCreatedEventConsumer, CustomerCreatedEvent>();
                });
        })
        .AddServicesFromAssembly(Assembly.GetExecutingAssembly())
        .AddJsonSerializer()
        .AddAsyncApiDocumentGenerator();
});

// doc:fragment:ExampleStartup2
// Add Saunter to the application services. 
builder.Services.AddAsyncApiSchemaGeneration(options =>
{
    options.AsyncApi = new AsyncApiDocument
    {
        Info = new Info("SlimMessageBus AsyncAPI Sample API", "1.0.0")
        {
            Description = "This is a sample of the SlimMessageBus AsyncAPI plugin",
            License = new License("Apache 2.0")
            {
                Url = "https://www.apache.org/licenses/LICENSE-2.0"
            }
        }
    };
});
// doc:fragment:ExampleStartup2

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Register AsyncAPI docs via Sauter 
app.MapAsyncApiDocuments();
app.MapAsyncApiUi();

app.Run();