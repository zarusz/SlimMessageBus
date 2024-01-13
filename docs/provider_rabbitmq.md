# RabbitMQ transport provider for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Underlying client](#underlying-client)
- [Concepts](#concepts)
- [Configuration](#configuration)
  - [Producers](#producers)
  - [Consumers](#consumers)
  - [Consumer Error Handling](#consumer-error-handling)
    - [Dead Letter Exchange](#dead-letter-exchange)
    - [Custom Consumer Error Handler](#custom-consumer-error-handler)
  - [Consumer Concurrency Level](#consumer-concurrency-level)
  - [Request-Response](#request-response)
- [Topology Provisioning](#topology-provisioning)
- [Not Supported](#not-supported)
- [Feedback](#feedback)

## Underlying client

The [`RabbitMQ`](https://www.nuget.org/packages/SlimMessageBus.Host.RabbitMQ) transport provider uses [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client/) client to connect to the RabbitMQ cluster via the AMQP protocol.

## Concepts

The RabbitMQ and AMQP protocol introduce couple of concepts:

- Exchange - entities to which producers send messages,
- Queue - mailboxes which consumers read messages from,
- Binding - are rules that exchanges use to route messages to queues.

[AMQP Concepts](https://www.rabbitmq.com/tutorials/amqp-concepts.html) provides a brilliant overview.

## Configuration

The RabbitMQ transport configuration is arranged via the `.WithProviderRabbitMQ(cfg => {})` method on the message bus builder.

```cs
using SlimMessageBus.Host.RabbitMQ;

// Register SlimMessageBus in MSDI
services.AddSlimMessageBus((mbb) =>
{
    // Use RabbitMQ transport provider
    mbb.WithProviderRabbitMQ(cfg =>
    {
        // Connect using AMQP URI
        cfg.ConnectionString = configuration["RabbitMQ:ConnectionString"];

        // Alternatively, when not using AMQP URI:
        // cfg.ConnectionFactory.HostName = "..."
        // cfg.ConnectionFactory.VirtualHost = "..."
        // cfg.ConnectionFactory.UserName = "..."
        // cfg.ConnectionFactory.Password = "..."
        // cfg.ConnectionFactory.Ssl.Enabled = true

        // Fine tune the underlying RabbitMQ.Client:
        // cfg.ConnectionFactory.ClientProvidedName = $"MyService_{Environment.MachineName}";
    });

    mbb.AddServicesFromAssemblyContaining<PingConsumer>();
    mbb.AddJsonSerializer();
});
```

The relevant elements of the `cfg`:

- The `ConnectionString` allows to set the AMQP URI.
  This property is a convenience wrapper on top of `ConnectionFactory.Uri` from the underlying client library.
  The URI has the following form: `amqps://<username>:<password>@<host>/<virtual-host>`.
- The `ConnectionFactory` allows to access other client settings. It can be used to setup other connection details in case the AMQP URI cannot be used or there is a need to fine tune the client. For more options see the underlying [RabbitMQ driver docs](https://www.rabbitmq.com/dotnet-api-guide.html#connecting).

### Producers

Producers need to declare the exchange name and type the message should be delivered to. SMB will provision the specified exchange.
Additionally, we can specify:

- the modifier that allows to assign message properties (`MessageId`, `ContentType`, and headers),
- the message key provider that is used in routing for relevant exchange types.

```cs
mbb.Produce<OrderEvent>(x => x
        // Will declare an orders exchange of type Fanout
        .Exchange("orders", exchangeType: ExchangeType.Fanout)
        // Will use a routing key provider that for a given message will take it's Id field
        .RoutingKeyProvider((m, p) => m.Id.ToString())
        // Will use
        .MessagePropertiesModifier((m, p) =>
        {
            p.MessageId = GetMessageId(m);
        }));
```

We can also set defaults for all producers on the bus level:

```cs
services.AddSlimMessageBus((mbb) =>
{
    mbb.WithProviderRabbitMQ(cfg =>
    {
        // All exchanges declared on producers will be durable by default
        cfg.UseExchangeDefaults(durable: true);

        // All messages will get the ContentType message property assigned
        cfg.UseMessagePropertiesModifier((m, p) =>
        {
            p.ContentType = MediaTypeNames.Application.Json;
        });
    });

    mbb.AddJsonSerializer();
});
```

### Consumers

Consumers need to specify the queue name from which the consumer should be reading from. SMB will provison the specified queue.
Additionally,

- when the exchange name binding is specified then SMB will provision that binding with the broker,
- when [dead letter exchange](#dead-letter-exchange) is specified then the queue will provisioned with the broker, and if the exchange type is specified it will also be provisioned.

```cs
mbb.Consume<PingMessage>(x => x
    // Use the subscriber queue, do not auto delete
    .Queue("subscriber", autoDelete: false)
    //
    .ExchangeBinding("ping")
    // The queue declaration in RabbitMQ will have a reference to the dead letter exchange and the DL exchange will be created
    .DeadLetterExchange("subscriber-dlq", exchangeType: ExchangeType: Direct)
    .WithConsumer<PingConsumer>());
```

We can specify defaults for all consumers on the bus level:

```cs
 services.AddSlimMessageBus((mbb) =>
{
    mbb.WithProviderRabbitMQ(cfg =>
    {
        cfg.UseDeadLetterExchangeDefaults(durable: false, autoDelete: false, exchangeType: ExchangeType.Direct, routingKey: string.Empty);
        cfg.UseQueueDefaults(durable: false);
    });
});
```

### Consumer Error Handling

By default the the transport implementation performs a negative ack (nack) in the AMQP protocol for any message that failed in the consumer. As a result the message will be marked as failed and routed to an dead letter exchange or discarded by the RabbitMQ broker.

The recommendation here is to either:

- configure a [dead letter exchange](#dead-letter-exchange) configured on the consumer queue,
- or provide a [custom error handler](#custom-consumer-error-handler) (retry the message couple of times, if failed send to a dead letter exchange).

#### Dead Letter Exchange

The [Dead Letter Exchanges](https://www.rabbitmq.com/dlx.html) is a feature of RabbitMQ that will forward failed messages from a particular queue to a special exchange.

In SMB on the consumer declaration we can specify which dead letter exchange should be used:

```cs
mbb.Consume<PingMessage>(x => x
    .Queue("subscriber", autoDelete: false)
    .ExchangeBinding(topic)
    // The queue provisioned in RabbitMQ will have a reference to the dead letter exchange
    .DeadLetterExchange("subscriber-dlq")
    .WithConsumer<PingConsumer>());
```

However, the `subscriber-dlq` will not be created by SMB in the sample. For it to be created the `ExchangeType` has to be specified, so that SMB knows what exchange type should it apply.
It can be specified on the consumer:

```cs
mbb.Consume<PingMessage>(x => x
    .Queue("subscriber", autoDelete: false)
    .ExchangeBinding(topic)
    // The queue provisioned in RabbitMQ will have a reference to the dead letter exchange and the DL exchange will be provisioned
    .DeadLetterExchange("subscriber-dlq", exchangeType: ExchangeType: Direct)
    .WithConsumer<PingConsumer>());
```

Alternatively, a bus wide default can be specified for all dead letter exchanges:

```cs
services.AddSlimMessageBus((mbb) =>
{
    mbb.WithProviderRabbitMQ(cfg =>
    {
        // All the declared dead letter exchanges on the consumers will be of Direct type
        cfg.UseDeadLetterExchangeDefaults(durable: false, autoDelete: false, exchangeType: ExchangeType.Direct, routingKey: string.Empty);
    });
});
```

#### Custom Consumer Error Handler

Define a custom consumer error handler implementation of `RabbitMqConsumerErrorHandler<>`:

```cs
public class CustomRabbitMqConsumerErrorHandler<T> : RabbitMqConsumerErrorHandler<T>
{
    // Inject needed dependencies via construction

    public override async Task<bool> OnHandleError(T message, IConsumerContext consumerContext, Exception exception)
    {
        // Check if this is consumer context for RabbitMQ
        var isRabbitMqContext = consumerContext.GetTransportMessage() != null;
        if (isRabbitMqContext)
        {
            if (exception is TransientException)
            {
                // Send negative acknowledge but ask the broker to retry
                consumerContext.NackWithRequeue();
            }
            else
            {
                // Send negative acknowledge (if dead letter setup it will be routed to it)
                consumerContext.Nack();
            }
            // Mark that the errored message was handled
            return true;
        }
        return false;
    }
}
```

Then register the implementation in MSDI for all (or specified) message types.

```cs
// Register error handler in MSDI for any message type
services.AddTransient(typeof(RabbitMqConsumerErrorHandler<>), typeof(CustomRabbitMqConsumerErrorHandler<>));
```

> When error handler is not found in the DI or it returns `false` then default error handling will be applied.

### Consumer Concurrency Level

By default each consumer in the service process will handle one message at the same time.
In order to increase the desired concurrency, set the [`ConsumerDispatchConcurrency`](https://www.rabbitmq.com/dotnet-api-guide.html#consumer-callbacks-and-ordering) to a value greater than 1.
This is a setting from the underlying RabbitMQ driver that SMB uses.

```cs
services.AddSlimMessageBus((mbb) =>
{
    mbb.WithProviderRabbitMQ(cfg =>
    {
        cfg.ConnectionFactory.ConsumerDispatchConcurrency = 2; // default is 1
        // ...
    }
}
```

> Notice that increasing concurrency will cause more messages to be processed at the same time within one service instance, hence affecting order of consumption.
> In scenarios where order of consumption is important, you may want to keep concurrency levels set to 1.

### Request-Response

Here is an example how to set-up request-response flow over RabbitMQ. The fanout exchange types was used, but other type could be used as well (altough we might have to provide the [routing key provider](#producers) on the producer side.)

```cs
services.AddSlimMessageBus((mbb) =>
{
    // ...
    mbb.Produce<EchoRequest>(x =>
    {
        // The requests should be send to "test-echo" exchange
        x.Exchange("test-echo", exchangeType: ExchangeType.Fanout);
    })
    .Handle<EchoRequest, EchoResponse>(x => x
        // Declare the queue for the handler
        .Queue("echo-request-handler")
        // Bind the queue to the "test-echo" exchange
        .ExchangeBinding("test-echo")
        // If the request handling fails, the failed messages will be routed to the DLQ exchange
        .DeadLetterExchange("echo-request-handler-dlq")
        .WithHandler<EchoRequestHandler>())
    .ExpectRequestResponses(x =>
    {
        // Tell the handler to which exchange send the responses to
        x.ReplyToExchange("test-echo-resp", ExchangeType.Fanout);
        // Which queue to use to read responses from
        x.Queue("test-echo-resp-queue");
        // Bind to the reply to exchange
        x.ExchangeBinding();
        // Timeout if the response doesn't arrive within 60 seconds
        x.DefaultTimeout(TimeSpan.FromSeconds(60));
    });
});
```

## Topology Provisioning

SMB automatically creates exchanges from producers, queues, dead letter exchanges and bindings from consumers.

However, if you need to layer on other topology elements (or peform cleanup) this could be achieved with `UseTopologyInitializer()`:

```cs
services.AddSlimMessageBus((mbb) =>
{
    mbb.WithProviderRabbitMQ(cfg =>
    {
        cfg.UseTopologyInitializer((channel, applyDefaultTopology) =>
        {
            // perform some cleanup if needed
            channel.QueueDelete("subscriber-0", ifUnused: true, ifEmpty: false);
            channel.QueueDelete("subscriber-1", ifUnused: true, ifEmpty: false);
            channel.ExchangeDelete("test-ping", ifUnused: true);
            channel.ExchangeDelete("subscriber-dlq", ifUnused: true);

            // apply default SMB infered topology
            applyDefaultTopology();
        });
    });
});
```

Avoiding the call `applyDefaultTopology()` will suppress the SMB inferred topology creation.
This might be useful in case the SMB inferred topology is not desired or there are other custom needs.

## Not Supported

- [Default type exchanges](https://www.rabbitmq.com/tutorials/amqp-concepts.html#exchange-default) are not yet supported
- Broker generated queues are not yet supported.

## Feedback

Open a github issue if you need a feature, have a suggestion for improvement, or want to contribute an enhancement.
