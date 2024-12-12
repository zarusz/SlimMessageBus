# Amazon SQS Provider for SlimMessageBus <!-- omit in toc -->

Before diving into this provider documentation, please make sure to read the [Introduction](intro.md).

### Table of Contents

- [Configuration](#configuration)
- [Amazon SNS](#amazon-sns)
- [Producing Messages](#producing-messages)
- [Consuming Messages](#consuming-messages)
  - [Consumer Context](#consumer-context)
- [Transport-Specific Settings](#transport-specific-settings)
- [Headers](#headers)
- [Request-Response Configuration](#request-response-configuration)
  - [Producing Request Messages](#producing-request-messages)
  - [Handling Request Messages](#handling-request-messages)
- [Topology Provisioning](#topology-provisioning)

## Configuration

To configure Amazon SQS as your transport provider, you need to specify the AWS region and choose an authentication method:

- **Static Credentials**: [Learn more](https://docs.aws.amazon.com/sdkref/latest/guide/access-iam-users.html)
- **Temporary Credentials**: [Learn more](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_credentials_temp_use-resources.html#RequestWithSTS)

```csharp
using SlimMessageBus.Host.AmazonSQS;
```

```cs
services.AddSlimMessageBus((mbb) =>
{
    mbb.WithProviderAmazonSQS(cfg =>
    {
        cfg.UseRegion(Amazon.RegionEndpoint.EUCentral1);

        // Use static credentials: https://docs.aws.amazon.com/sdkref/latest/guide/access-iam-users.html
        cfg.UseCredentials(accessKey, secretAccessKey);

        // Use temporary credentials: https://docs.aws.amazon.com/IAM/latest/UserGuide/id_credentials_temp_use-resources.html#RequestWithSTS
        //cfg.UseTemporaryCredentials(roleArn, roleSessionName);

        AdditionalSqsSetup(cfg);
    });
});
```

For an example configuration, check out this file: [`SqsMessageBusSettings`](../src/SlimMessageBus.Host.AmazonSQS/SqsMessageBusSettings.cs). The settings allow you to customize the SQS client object and control topology provisioning for advanced scenarios.

## Amazon SNS

Support for Amazon SNS (Simple Notification Service) will be added soon to this transport plugin.

## Producing Messages

To produce a `TMessage` to an Amazon SQS queue or an SNS topic:

```csharp
// Send TMessage to Amazon SQS queue
mbb.Produce<TMessage>(x => x.UseQueue());

// OR

// Send TMessage to Amazon SNS topic
mbb.Produce<TMessage>(x => x.UseTopic());
```

This configuration sends `TMessage` to the specified Amazon SQS queue or SNS topic. You can then produce messages like this:

```csharp
TMessage msg;

// Send msg to "some-queue"
await bus.Publish(msg, "some-queue");

// OR

// Send msg to "some-topic"
await bus.Publish(msg, "some-topic");
```

The second parameter (`path`) specifies the name of the queue or topic to use.

If you have a default queue or topic configured for a message type:

```csharp
mbb.Produce<TMessage>(x => x.DefaultQueue("some-queue"));

// OR

mbb.Produce<TMessage>(x => x.DefaultTopic("some-topic"));
```

You can simply call `bus.Publish(msg)` without providing the second parameter:

```csharp
// Send msg to the default queue "some-queue" (or "some-topic")
bus.Publish(msg);
```

Note that if no explicit configuration is provided, the system assumes the message will be sent to a topic (equivalent to using `UseTopic()`).

## Consuming Messages

To consume messages of type `TMessage` by `TConsumer` from an Amazon SNS topic named `some-topic`:

```csharp
mbb.Consume<TMessage>(x => x
   .Queue("some-topic")
   //.WithConsumer<TConsumer>());
```

To consume messages from an Amazon SQS queue named `some-queue`:

```csharp
mbb.Consume<TMessage>(x => x
   .Queue("some-queue")
   //.WithConsumer<TConsumer>());
```

### Consumer Context

The consumer can implement the `IConsumerWithContext` interface to access native Amazon SQS messages:

```csharp
public class PingConsumer : IConsumer<PingMessage>, IConsumerWithContext
{
   public IConsumerContext Context { get; set; }

   public Task OnHandle(PingMessage message, CancellationToken cancellationToken)
   {
      // Access the native Amazon SQS message:
      var transportMessage = Context.GetTransportMessage(); // Amazon.SQS.Model.Message type
   }
}
```

This can be helpful to extract properties like `MessageId` or `Attributes` from the native SQS message.

## Transport-Specific Settings

Producer and consumer configurations have additional settings like:

- **EnableFifo**
- **MaxMessageCount**

For a producer:

```csharp
mbb.Produce<TMessage>(x => x
   .EnableFifo(f => f
     .DeduplicationId((m, h) => (m.Counter + 1000).ToString())
     .GroupId((m, h) => m.Counter % 2 == 0 ? "even" : "odd")
   )
);
```

For a consumer:

```csharp
mbb.Consume<TMessage>(x => x
   .WithConsumer<TConsumer>()
   .Queue("some-queue")
   .MaxMessageCount(10)
   .Instances(1));
```

These default values can also be set at the message bus level using `SqsMessageBusSettings`:

```csharp
mbb.WithProviderAmazonSQS(cfg =>
{
   cfg.MaxMessageCount = 10;
});
```

Settings at the consumer level take priority over the global defaults.

## Headers

Amazon SQS has specific requirements for message headers, detailed in [this guide](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-message-metadata.html#sqs-message-attributes).

Headers in SlimMessageBus (SMB) can be any type (`object`). To convert them, SMB uses a header serializer, like the default implementation: [DefaultSqsHeaderSerializer](../src/SlimMessageBus.Host.AmazonSQS/Headers/DefaultSqsHeaderSerializer.cs).

By default, string values are attempted to be converted to `Guid`, `bool`, and `DateTime`.

You can override the serializer through `SqsMessageBusSettings`.

## Request-Response Configuration

### Producing Request Messages

When sending a request, you need to specify whether it should be delivered to a topic or queue (see [Producing Messages](#producing-messages)).

To receive responses on a queue:

```csharp
mbb.ExpectRequestResponses(x =>
{
    x.ReplyToQueue("test-echo-queue-resp");
    x.DefaultTimeout(TimeSpan.FromSeconds(60));
});
```

Or to receive responses on a topic:

```csharp
mbb.ExpectRequestResponses(x =>
{
    x.ReplyToTopic("test-echo-resp");
    x.DefaultTimeout(TimeSpan.FromSeconds(60));
});
```

Each service instance should have a dedicated queue or topic to ensure the response arrives back at the correct instance. This ensures the internal task `Task<TResponse>` of `bus.Send(TRequest)` resumes correctly.

### Handling Request Messages

For services handling requests, you must configure the request consumption settings for a specific queue (or topic):

Example for queue:

```csharp
mbb.Handle<EchoRequest, EchoResponse>(x => x
   .Queue(queue)
   .WithHandler<EchoRequestHandler>());
```

Example for topic:

```csharp
mbb.Handle<EchoRequest, EchoResponse>(x => x
   .Topic(topic)
   .SubscriptionName("handler")
   .WithHandler<EchoRequestHandler>());
```

Ensure that if a request is sent to a queue, the consumer is also listening on that queue (and similarly for topics). Mixing queues and topics for requests and responses is not supported.

## Topology Provisioning

Amazon SQS can automatically create any queues declared in your SMB configuration when needed. This process occurs when the SMB instance starts, and only for queues that do not yet exist. If a queue already exists, it will not be modified.

> **Note**: Automatic topology creation is enabled by default.

To disable automatic topology provisioning:

```csharp
mbb.WithProviderAmazonSQS(cfg =>
{
   cfg.TopologyProvisioning.Enabled = false;
});
```

You can also customize how queues are created by modifying the `CreateQueueOptions`:

```csharp
mbb.WithProviderAmazonSQS(cfg =>
{
   cfg.TopologyProvisioning = new()
   {
      CreateQueueOptions = (options) =>
      {
         // Customize queue options here
      }
   };
});
```

You can control which services create queues:

```csharp
mbb.WithProviderAmazonSQS(cfg =>
{
   cfg.TopologyProvisioning = new()
   {
      Enabled = true,
      CanProducerCreateQueue = true,  // Only producers can create queues
      CanConsumerCreateQueue = false, // Consumers cannot create queues
   };
});
```

> By default, both flags are enabled (`true`).

This flexibility allows you to define ownership of queues/topics clearly—e.g., producers handle queue creation while consumers manage subscriptions.