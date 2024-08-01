using NATS.Client.Core;

using Sample.Nats.WebApi;

using SecretStore;

using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Nats.Config;
using SlimMessageBus.Host.Serialization.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

Secrets.Load(@"..\..\..\..\..\secrets.txt");

var endpoint = Secrets.Service.PopulateSecrets(builder.Configuration["Nats:Endpoint"]);
var topic = Secrets.Service.PopulateSecrets(builder.Configuration["Nats:Topic"]);

// doc:fragment:ExampleConfiguringMessageBus
builder.Services.AddSlimMessageBus(messageBusBuilder =>
{
    messageBusBuilder.WithProviderNats(cfg =>
    {
        cfg.Endpoint = endpoint;
        cfg.ClientName = $"MyService_{Environment.MachineName}";
        cfg.AuthOpts = NatsAuthOpts.Default;
    });

    messageBusBuilder
        .Produce<PingMessage>(x => x.DefaultTopic(topic))
        .Consume<PingMessage>(x => x.Topic(topic).Instances(1));

    messageBusBuilder.AddServicesFromAssemblyContaining<PingConsumer>();
    messageBusBuilder.AddJsonSerializer();
});
// doc:fragment:ExampleConfiguringMessageBus

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/publish-message", (IMessageBus messageBus, CancellationToken cancellationToken) =>
{
    PingMessage pingMessage = new(0, Guid.NewGuid());
    messageBus.Publish(pingMessage, cancellationToken: cancellationToken);
});

app.Run();