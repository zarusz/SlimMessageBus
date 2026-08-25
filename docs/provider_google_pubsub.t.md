+# SlimMessageBus - Google Cloud Pub/Sub Transport <!-- omit in toc -->

Before using this provider, read the [Introduction](intro.md).

## Installation

Install the transport and a serializer:

```shell
dotnet add package SlimMessageBus.Host.GooglePubSub
dotnet add package SlimMessageBus.Host.Serialization.SystemTextJson
```

Then import the provider namespace:

```csharp
using SlimMessageBus.Host.GooglePubSub;
```

This provider supports standard Google Cloud Pub/Sub topics and pull subscriptions. Pub/Sub Lite is not supported because Google shut down that service on March 18, 2026.

## Basic configuration

```csharp
services.AddSlimMessageBus(mbb =>
{
    mbb.WithProviderGooglePubSub(settings =>
    {
        settings.ProjectId = "my-google-cloud-project";
    });

    mbb.AddJsonSerializer();

    mbb.Produce<OrderCreated>(x => x
        .DefaultTopic("orders"));

    mbb.Consume<OrderCreated>(x => x
        .Topic("orders")
        .SubscriptionName("orders-billing")
        .WithConsumer<BillingConsumer>());
});
```

`ProjectId` is used to turn short IDs such as `orders` into full Google resource names. Fully qualified names such as `projects/another-project/topics/orders` are also accepted.

Each consumer needs a subscription name. Google subscription IDs are unique within a project and a subscription belongs to exactly one topic, so use a distinct subscription for each independently consuming application or consumer group.

## Authentication

### Application Default Credentials

[Application Default Credentials (ADC)](https://cloud.google.com/docs/authentication/application-default-credentials) are used automatically when no client factories or explicit credentials are configured. This is the recommended configuration.

ADC searches for credentials in Google's standard order, including:

- the service account attached to the Google Cloud runtime;
- credentials referenced by the `GOOGLE_APPLICATION_CREDENTIALS` environment variable;
- local development credentials created by `gcloud auth application-default login`.

On Google Cloud, attach a service account to the workload and grant only the roles it requires. Applications that publish and consume normally need `roles/pubsub.publisher` and `roles/pubsub.subscriber`. Because topology provisioning is enabled by default, the identity also needs permission to inspect and create topics and subscriptions, for example `roles/pubsub.editor`. If infrastructure is provisioned separately, disable topology provisioning and omit the administrative role.

### Credential JSON file

A trusted service-account credential file can be selected explicitly:

```csharp
mbb.WithProviderGooglePubSub(settings =>
{
    settings.ProjectId = "my-google-cloud-project";
    settings.UseCredentialsPath(configuration["GoogleCloud:CredentialsPath"]);
});
```

Do not commit credential JSON files to source control. Prefer workload identity or an attached service account in production.

An already constructed `GoogleCredential` can also be supplied (`using Google.Apis.Auth.OAuth2;`):

```csharp
GoogleCredential credential = await GoogleCredential.GetApplicationDefaultAsync();

mbb.WithProviderGooglePubSub(settings =>
{
    settings.ProjectId = "my-google-cloud-project";
    settings.UseGoogleCredential(credential);
});
```

The four client factories on `GooglePubSubMessageBusSettings` can be replaced when credentials, endpoints, channel counts, or other native client settings need to be controlled separately.

### API keys

[Google Cloud Pub/Sub does not support API keys](https://cloud.google.com/pubsub/docs/authentication) as an authentication method. Use OAuth 2.0 credentials through ADC, a service account, workload identity federation, or another `GoogleCredential`.

## Producing messages

Declare a default topic and publish normally:

```csharp
mbb.Produce<OrderCreated>(x => x.DefaultTopic("orders"));

await bus.Publish(new OrderCreated(...));
```

A native `PubsubMessage` can be customized globally or per producer:

```csharp
mbb.WithProviderGooglePubSub(settings =>
{
    settings.ProjectId = "my-google-cloud-project";
    settings.WithModifier((message, transportMessage) =>
    {
        transportMessage.Attributes["application"] = "billing";
    });
});

mbb.Produce<OrderCreated>(x => x
    .DefaultTopic("orders")
    .WithModifier((message, transportMessage) =>
    {
        transportMessage.OrderingKey = message.CustomerId;
    }));
```

If ordering keys are used, configure the native publisher to enable ordering and enable ordering on the provisioned subscription:

```csharp
mbb.WithProviderGooglePubSub(settings =>
{
    settings.ProjectId = "my-google-cloud-project";
    settings.PublisherClientFactory = (_, topic, cancellationToken) =>
        new PublisherClientBuilder
        {
            TopicName = topic,
            Settings = new PublisherClient.Settings
            {
                EnableMessageOrdering = true
            }
        }.BuildAsync(cancellationToken);
});

mbb.Consume<OrderCreated>(x => x
    .Topic("orders")
    .SubscriptionName("orders-billing")
    .CreateSubscriptionOptions(subscription =>
    {
        subscription.EnableMessageOrdering = true;
    })
    .WithConsumer<BillingConsumer>());
```

Publisher clients are cached per topic and disposed with the bus.

## Consuming messages

Consumers use Google pull subscriptions:

```csharp
mbb.Consume<OrderCreated>(x => x
    .Topic("orders")
    .SubscriptionName("orders-billing")
    .Instances(8)
    .WithConsumer<BillingConsumer>());
```

Successful processing acknowledges the Pub/Sub message. A failed message is negatively acknowledged and becomes eligible for redelivery according to its subscription retry and dead-letter policies.

The native message and subscription name are available from `IConsumerContext`:

```csharp
public class BillingConsumer(IConsumerContext context) : IConsumer<OrderCreated>
{
    public Task OnHandle(OrderCreated message, CancellationToken cancellationToken)
    {
        PubsubMessage transportMessage = context.GetTransportMessage();
        string subscriptionName = context.GetSubscriptionName();

        return Task.CompletedTask;
    }
}
```

A transport-specific error handler can implement `IGooglePubSubConsumerErrorHandler<T>` or derive from `GooglePubSubConsumerErrorHandler<T>`.

## Request-response

Configure a reply topic and a dedicated subscription for the application instance:

```csharp
mbb.ExpectRequestResponses(x => x
    .ReplyToTopic("billing-responses")
    .SubscriptionName("billing-api-instance-1")
    .DefaultTimeout(TimeSpan.FromSeconds(30)));
```

The request handler publishes responses to the reply topic. The requester receives them through the configured pull subscription. Each simultaneously running requester needs its own subscription name.

## Topology provisioning

Topology provisioning is enabled by default. At startup the provider creates missing declared topics and subscriptions; it does not update existing resources.

Disable it when Terraform, Pulumi, Deployment Manager, or another system owns the infrastructure:

```csharp
mbb.WithProviderGooglePubSub(settings =>
{
    settings.ProjectId = "my-google-cloud-project";
    settings.TopologyProvisioning.Enabled = false;
});
```

Creation permissions can be controlled independently:

```csharp
settings.TopologyProvisioning.CanProducerCreateTopic = false;
settings.TopologyProvisioning.CanConsumerCreateTopic = true;
settings.TopologyProvisioning.CanConsumerCreateSubscription = true;
```

Global native creation options apply to every newly created resource:

```csharp
settings.TopologyProvisioning.CreateTopicOptions = topic =>
{
    topic.Labels["application"] = "billing";
};

settings.TopologyProvisioning.CreateSubscriptionOptions = subscription =>
{
    subscription.AckDeadlineSeconds = 60;
};
```

They can be refined on individual declarations (`using Google.Protobuf.WellKnownTypes;` for `Duration`):

```csharp
mbb.Consume<OrderCreated>(x => x
    .Topic("orders")
    .SubscriptionName("orders-billing")
    .CreateSubscriptionOptions(subscription =>
    {
        subscription.Filter = "attributes.region = \"eu\"";
        subscription.EnableExactlyOnceDelivery = true;
        subscription.RetryPolicy = new RetryPolicy
        {
            MinimumBackoff = Duration.FromTimeSpan(TimeSpan.FromSeconds(10)),
            MaximumBackoff = Duration.FromTimeSpan(TimeSpan.FromMinutes(5))
        };
        subscription.DeadLetterPolicy = new DeadLetterPolicy
        {
            DeadLetterTopic = "projects/my-google-cloud-project/topics/orders-dead-letter",
            MaxDeliveryAttempts = 10
        };
    })
    .WithConsumer<BillingConsumer>());
```

Only pull subscriptions are supported. Configuring push, BigQuery, or Cloud Storage delivery on a SlimMessageBus consumer is rejected.

## Headers

Pub/Sub attributes contain string values, while SlimMessageBus headers can contain objects. `DefaultGooglePubSubHeaderSerializer` adds type markers for common primitives, GUIDs, `DateTime`, and `DateTimeOffset` so their types survive a round trip. Plain string values remain unchanged.

Replace `GooglePubSubMessageBusSettings.HeaderSerializer` with an `IGooglePubSubHeaderSerializer` implementation when another wire format is required.

## Emulator

Set `PUBSUB_EMULATOR_HOST` and replace the four client factories with builders configured with `EmulatorDetection.EmulatorOrProduction`. The factories are exposed on `GooglePubSubMessageBusSettings` so publisher, subscriber, and topology clients can all use the same emulator endpoint.
