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
