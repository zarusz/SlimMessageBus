# MQTT transport provider for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Underlying MQTT client](#underlying-mqtt-client)
- [Configuration](#configuration)
- [Topic matching](#topic-matching)
- [Message Headers](#message-headers)

## Underlying MQTT client

This transport provider uses [MQTTnet](https://www.nuget.org/packages/MQTTnet) client to connect to the MQTT broker.

## Configuration

The configuration is arranged via the `.WithProviderMqtt(cfg => {})` method on the message bus builder.

```cs
services.AddSlimMessageBus(mbb =>
{
    mbb.WithProviderMqtt(cfg =>
    {
        cfg.ClientBuilder
            .WithTcpServer(configuration["Mqtt:Server"], int.Parse(configuration["Mqtt:Port"]))
            .WithTlsOptions(o => o.UseTls())
            .WithCredentials(configuration["Mqtt:Username"], configuration["Mqtt:Password"])
            // Use MQTTv5 to use message headers (if the broker supports it)
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500);
    });

    mbb.AddServicesFromAssemblyContaining<PingConsumer>();
    mbb.AddJsonSerializer();
});
```

The `ClientBuilder` property (of type `MqttClientOptionsBuilder`) is used to configure the underlying [MQTTnet library client](https://github.com/dotnet/MQTTnet/wiki/Client).
Please consult the MQTTnet library docs for more configuration options.

## Topic matching

Consumer topics can contain MQTT wildcard filters such as `sensors/+/temperature` or `sensors/#`. Wildcard matching follows MQTTnet's topic filter comparer.

When filters overlap, the first matching subscription in consumer creation order is selected. An exact topic does not take precedence over an earlier matching wildcard. Request-response reply subscriptions participate in the same matching order.

Exact topics are indexed when consumers are created. Without wildcard subscriptions, each incoming topic uses a dictionary lookup instead of scanning all consumers. With wildcards, only wildcard filters preceding a matching exact topic need comparison; if no exact topic exists, all wildcard filters may be checked. The index contains configured subscriptions only, so its memory use does not grow with the number of distinct incoming topics. It is rebuilt when consumers are recreated.

## Message Headers

Headers are only supported with MQTTv5 protocol version.
Having a MQTTv5 compliant broker, you still need to to enable the protocol v5 version on the client to enable header feature:

```cs
services.AddSlimMessageBus(mbb =>
{
    mbb.WithProviderMqtt(cfg =>
    {
        cfg.ClientBuilder
            // ...
            // Use MQTTv5 to use message headers (if the broker supports it)
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500);
    });
});
```

> [Request-response](intro.md#request-response-communication) relies on the presence of headers. When headers are not availabe, then request-response communication will not work with SMB.

In the future SMB might introduce headers emulation (similar to the [Redis transport](provider_redis.md#message-headers)) for earlier protcol versions.
If you have need this feature plase raise an issue.