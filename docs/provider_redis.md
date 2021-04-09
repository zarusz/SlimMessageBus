# Redis transport provider for SlimMessageBus

## Introduction

Please read the [Introduction](intro.md) before reading this provider documentation.

## Configuration

Redis transport provider requires a connection string:

```cs
var connectionString = "server1:6379,server2:6379" // Redis connection string

MessageBusBuilder mbb = MessageBusBuilder
    .Create()
    // the bus configuration here
    .WithProviderRedis(new RedisMessageBusSettings(connectionString))
    .WithSerializer(new JsonMessageSerializer());

IMessageBus bus = mbb.Build();
```

The `RedisMessageBusSettings` has additional settings that allow to override factories for the `ConnectionMultiplexer`. This may be used for some advanced scenarios.

## Connection string parameters

The list of all configuration parameters for the connectiong string can be found here:
https://stackexchange.github.io/StackExchange.Redis/Configuration

## Underlying Redis client

This transport porider uses [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis)

## Consumer

Redis Pub/Sub does not allow to have multiple named subscriptions under one topic that the same client could use (see [here](https://redis.io/topics/pubsub)). Instead each redis client is an individual subscriber of a given topic.

Consider a micro-service that performs the following SMB registration and uses the SMB Redis transport:

```cs
var mbb = MessageBusBuilder.Create();

mbb
  .Produce<SomeMessage>(x => x.DefaultTopic("some-topic"))
  .Consume<SomeMessage>(x => x
    .Topic("some-topic") // redis topic name
    .WithConsumer<SomeConsumer>()

```

If there are 3 instances of that micro-service running, and one of them publishes the `SomeMessage`:

```cs
await bus.Publish(new SomeMessage())
```

Then all 3 service instances will have the message copy delivered to the `SomeConsumer` (even the service instance that published the message in the first place).
This is because each service instance is an independent subscriber (independent client).
