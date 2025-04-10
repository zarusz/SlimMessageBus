# Nats transport provider for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Underlying NATS client](#underlying-nats-client)
- [Configuration](#configuration)
- [Message Serialization](#message-serialization)

## Underlying NATS client

This transport provider uses [NATS.Nets](https://www.nuget.org/packages/NATS.Net) client to connect to the NATS broker.

## Configuration

The configuration is arranged via the `.WithProviderNats(cfg => {})` method on the message bus builder.

```cs
builder.Services.AddSlimMessageBus(mbb =>
{
    mbb.WithProviderNats(cfg =>
    {
        cfg.Endpoint = endpoint;
        cfg.ClientName = $"MyService_{Environment.MachineName}";
        cfg.AuthOpts = NatsAuthOpts.Default;
    });

    mbb
        .Produce<PingMessage>(x => x.DefaultTopic(topic))
        .Consume<PingMessage>(x => x.Topic(topic).Instances(1));

    mbb.AddServicesFromAssemblyContaining<PingConsumer>();
    mbb.AddJsonSerializer();
});
```

The `NatsMessageBusSettings` property is used to configure the underlying [Nats.Net library client](https://github.com/nats-io/nats.net).
Please consult the NATS.Net library docs for more configuration options.

See the [full sample](/src/Samples/Sample.Nats.WebApi/).

## Message Serialization

Nats offers native serialization functionality. This functionality conflicts with the serialization functionality provided by SlimMessageBus. We have chosen to leave the responsibility for serialization to SlimMessageBus and leave the default configuration of Nats serialization, which is raw serialization. This means that the message body is serialized as a byte array.