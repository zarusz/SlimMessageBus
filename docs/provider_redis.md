# Redis transport provider for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Underlying Redis client](#underlying-redis-client)
- [Configuration](#configuration)
  - [Connection string parameters](#connection-string-parameters)
- [Producer](#producer)
- [Consumer](#consumer)
  - [Pub/Sub](#pubsub)
  - [Queues](#queues)
- [Queue implementation on Redis](#queue-implementation-on-redis)
  - [Message Headers](#message-headers)
  - [Redis transport lifecycle hooks](#redis-transport-lifecycle-hooks)

## Underlying Redis client

This transport provider uses [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis) client to connect to Redis.

## Configuration

Redis transport provider requires a connection string:

```cs
var connectionString = "server1:6379,server2:6379" // Redis connection string

// MessageBusBuilder mbb;
mbb.    
    // the bus configuration here
    .WithProviderRedis(new RedisMessageBusSettings(connectionString))
    .WithSerializer(new JsonMessageSerializer());
```

The `RedisMessageBusSettings` has additional settings that allow to override factories for the `ConnectionMultiplexer`. This may be used for some advanced scenarios.

### Connection string parameters

The list of all configuration parameters for the connectiong string can be found here:
https://stackexchange.github.io/StackExchange.Redis/Configuration

## Producer

To produce a given `TMessage` to Redis pub/sub topic (or queue implemented as a Redis list) use:

```cs
// send TMessage to Redis queues (lists)
mbb.Produce<TMessage>(x => x.UseQueue()); 

// send TMessage to Redis pub/sub topics
mbb.Produce<TMessage>(x => x.UseTopic());
```

This configures the runtime to deliver all messages of type `TMessage` to a Redis pub/sub topic (or queue).

Then anytime you produce a message like this:

```cs
TMessage msg;

// msg will go to the "some-topic" topic
bus.Publish(msg, "some-topic");

// OR

// msg will go to the "some-queue" queue
bus.Publish(msg, "some-queue");
```

The second (`path`) parameter will be interpreted as either a queue name or topic name - depending on the bus configuration.

If you configure the default queue (or default topic) for a message type:

```cs
mbb.Produce<TMessage>(x => x.DefaultTopic("some-topic"));
// OR
mbb.Produce<TMessage>(x => x.DefaultQueue("some-queue"));    
```

and skip the second (name) parameter in `bus.Publish()`, then that default queue (or default topic) name is going to be used:

```cs
// msg will go to the "some-queue" queue (or "some-topic")
bus.Publish(msg);
```

Setting the default queue name `DefaultQueue()` for a message type will implicitly configure `UseQueue()` for that message type. By default if no configuration is present the runtime will assume a message needs to be sent on a topic (and works as if `UseTopic()` was configured).

## Consumer

### Pub/Sub

Unlike other messaging brokers implementing pub/sub, Redis Pub/Sub does not allow to have multiple named subscriptions for a topic and the same client (see [here](https://redis.io/topics/pubsub)). Instead each redis client becomes an individual subscriber of a topic when the client subscribes.

Consider a micro-service that performs the following SMB registration and uses the SMB Redis transport:

```cs
var mbb = MessageBusBuilder.Create();

mbb
  .Produce<SomeMessage>(x => x.DefaultTopic("some-topic"))
  .Consume<SomeMessage>(x => x
    .Topic("some-topic") // Redis topic name
    .WithConsumer<SomeConsumer>()

```

If there are 3 instances of that micro-service running, and one of them publishes the `SomeMessage`:

```cs
await bus.Publish(new SomeMessage())
```

Then all 3 service instances will have the message copy delivered to the `SomeConsumer` (even the service instance that published the message in question).
This is because each service instance is an independent subscriber (independent Redis client).

> In redis pub/sub the published messages are not durable. At the time of publish only connected consumers will recieve the message. If any of your service instances comes online after the publish (had a downtime, was restarted) the previously publishied messages will not be delivered.

### Queues

To consume `TMessage` by `TConsumer` from `some-queue` Redis queue (list) use:

```cs
mbb.Consume<TMessage>(x => x
    .WithConsumer<TConsumer>()
    .Queue("some-queue")
    .Instances(1));
```

## Queue implementation on Redis

The queue is emulated using a Redis list type (the key represents the queue name and the value is a Redis list type).
Internally when a new message is added to a queue `some-queue` then message bus will:

- producer will use the [`RPUSH`](https://redis.io/commands/rpush) to add the message at the tail of the list with key `some-queue`
- consumer will use the [`LPOP`](https://redis.io/commands/lpop) to remove the massage from the head of the list with key `some-queue`

> The above implementation provides at-most-once delivery guarantee. 

There is a chance that the consumer process dies after it performs `LPOP` and before it fully processes the message.

Another implementation was also considered using [`RPOPLPUSH`](https://redis.io/commands/rpoplpush) that would allow for at-least-once quarantee. 
However, that would require to manage individual per process instance local queues making tha runtime and configuration not practical.

### Message Headers

SMB uses headers to pass additional metadata information with the message. This includes the `MessageType` (of type `string`) or in the case of request/response messages the `RequestId` (of type `string`), `ReplyTo` (of type `string`) and `Expires` (of type `long`).
Redis does not support headers natively hence SMB Redis transport emulates them.
The emulation works by using a message wrapper envelope (`MessageWithHeader`) that during serialization puts the headers first and then the actual message content after that. If you want to override that behaviour, you could provide another serializer as long as it is able to serialize the wrapper `MessageWithHeaders` type:

```cs
mbb.WithProviderRedis(new RedisMessageBusSettings(redisConnectionString) 
{ 
    EnvelopeSerializer = new MessageWithHeadersSerializer() 
});
```

### Redis transport lifecycle hooks

Redis transport provider has also an additional hook that allows to perform some initialization once the connection to Redis database object (`IDatabase`) is established by SMB:

```cs
mbb.WithProviderRedis(new RedisMessageBusSettings(connectionString)
{
    OnDatabaseConnected = (database) =>
    {
        // Upon connect clear the redis list with the specified keys
        database.KeyDelete("test-echo-queue");
        database.KeyDelete("test-echo-queue-resp");
    }
});
```
