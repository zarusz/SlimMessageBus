namespace SlimMessageBus.Host.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using SlimMessageBus.Host.AzureServiceBus.Consumer;
using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Config;

public class ServiceBusMessageBus : MessageBusBase
{
    private readonly ILogger logger;

    public ServiceBusMessageBusSettings ProviderSettings { get; }

    private ServiceBusClient client;
    private SafeDictionaryWrapper<string, ServiceBusSender> producerByPath;

    private readonly List<AsbBaseConsumer> consumers = new();

    private Task provisionTopologyTask = null;

    public ServiceBusMessageBus(MessageBusSettings settings, ServiceBusMessageBusSettings providerSettings)
        : base(settings)
    {
        logger = LoggerFactory.CreateLogger<ServiceBusMessageBus>();
        ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));

        OnBuildProvider();
    }

    protected override void AssertSettings()
    {
        base.AssertSettings();

        var kindMapping = new KindMapping();
        // This will validae if one path is mapped to both a topic and a queue
        kindMapping.Configure(Settings);
    }


    protected override void AssertConsumerSettings(ConsumerSettings consumerSettings)
    {
        if (consumerSettings is null) throw new ArgumentNullException(nameof(consumerSettings));

        base.AssertConsumerSettings(consumerSettings);

        Assert.IsTrue(consumerSettings.PathKind != PathKind.Topic || consumerSettings.GetSubscriptionName(required: false) != null,
            () => new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(SettingsExtensions.SubscriptionName)} is not set on topic {consumerSettings.Path}"));
    }

    protected void AddConsumer(TopicSubscriptionParams topicSubscription, IEnumerable<IMessageProcessor<ServiceBusReceivedMessage>> consumers)
    {
        if (topicSubscription is null) throw new ArgumentNullException(nameof(topicSubscription));
        if (consumers is null) throw new ArgumentNullException(nameof(consumers));

        logger.LogInformation("Creating consumer for Path: {Path}, SubscriptionName: {SubscriptionName}", topicSubscription.Path, topicSubscription.SubscriptionName);
        AsbBaseConsumer consumer = topicSubscription.SubscriptionName != null
            ? new AsbTopicSubscriptionConsumer(this, consumers, topicSubscription, client)
            : new AsbQueueConsumer(this, consumers, topicSubscription, client);

        this.consumers.Add(consumer);
    }

    #region Overrides of MessageBusBase

    protected override void Build()
    {
        base.Build();

        if (ProviderSettings.TopologyProvisioning?.Enabled ?? false)
        {
            provisionTopologyTask = ProvisionTopology(); // provisining happens asynchronously
            BeforeStartTask = provisionTopologyTask;
        }

        client = ProviderSettings.ClientFactory();

        producerByPath = new SafeDictionaryWrapper<string, ServiceBusSender>(path =>
        {
            logger.LogDebug("Creating sender for path {Path}", path);
            return ProviderSettings.SenderFactory(path, client);
        });

        static MessageWithHeaders messageProvider(ServiceBusReceivedMessage m) => new(m.Body.ToArray(), m.ApplicationProperties.ToDictionary(x => x.Key, x => x.Value));
        static void initConsumerContext(ServiceBusReceivedMessage m, ConsumerContext ctx) => ctx.SetTransportMessage(m);

        logger.LogInformation("Creating consumers");

        foreach (var consumerSettingsByPath in Settings.Consumers.GroupBy(x => (x.Path, x.PathKind, SubscriptionName: x.GetSubscriptionName(required: false))))
        {
            var key = consumerSettingsByPath.Key;

            var consumers = consumerSettingsByPath.Select(x => new ConsumerInstanceMessageProcessor<ServiceBusReceivedMessage>(x, this, messageProvider, initConsumerContext)).ToList();
            AddConsumer(new TopicSubscriptionParams(key.Path, key.SubscriptionName), consumers);
        }

        if (Settings.RequestResponse != null)
        {
            var (path, pathKind, subscriptionName) = (Settings.RequestResponse.Path, Settings.RequestResponse.PathKind, Settings.RequestResponse.GetSubscriptionName(required: false));

            var consumers = new[]
            {
                new ResponseMessageProcessor<ServiceBusReceivedMessage>(Settings.RequestResponse, this, messageProvider)
            };
            AddConsumer(new TopicSubscriptionParams(path, subscriptionName), consumers);
        }
    }

    private async Task ProvisionTopology()
    {
        try
        {
            logger.LogInformation("Topology provisioning started...");

            var adminClient = ProviderSettings.AdminClientFactory();

            async Task<bool> SwallowExceptionIfEntityExists(Func<Task<bool>> task)
            {
                try
                {
                    return await task();
                }
                catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    // do nothing as another service instance might have created that in the meantime
                    return false;
                }
            }

            Task<bool> TryCreateQueue(string path, Action<CreateQueueOptions> action) => SwallowExceptionIfEntityExists(async () =>
            {
                if (!await adminClient.QueueExistsAsync(path))
                {
                    var options = new CreateQueueOptions(path);
                    ProviderSettings.TopologyProvisioning?.CreateQueueOptions?.Invoke(options);
                    action?.Invoke(options);

                    logger.LogInformation("Creating queue: {Path} ...", path);
                    await adminClient.CreateQueueAsync(options);
                    return true;
                }
                return false;
            });

            Task<bool> TryCreateTopic(string path, Action<CreateTopicOptions> action) => SwallowExceptionIfEntityExists(async () =>
            {
                if (!await adminClient.TopicExistsAsync(path))
                {
                    var options = new CreateTopicOptions(path);
                    ProviderSettings.TopologyProvisioning?.CreateTopicOptions?.Invoke(options);
                    action?.Invoke(options);

                    logger.LogInformation("Creating topic: {Path} ...", path);
                    await adminClient.CreateTopicAsync(options);
                    return true;
                }
                return false;
            });

            Task<bool> TryCreateSubscription(string path, string subscriptionName, Action<CreateSubscriptionOptions> action) => SwallowExceptionIfEntityExists(async () =>
            {
                if (!await adminClient.SubscriptionExistsAsync(path, subscriptionName))
                {
                    var options = new CreateSubscriptionOptions(path, subscriptionName);
                    ProviderSettings.TopologyProvisioning?.CreateSubscriptionOptions?.Invoke(options);
                    action?.Invoke(options);

                    logger.LogInformation("Creating subscription: {SubscriptionName} on topic: {Path} ...", subscriptionName, path);
                    await adminClient.CreateSubscriptionAsync(options);
                    return true;
                }
                return false;
            });

            Task<bool> TryCreateRule(string path, string subscriptionName, string ruleName, Action<CreateRuleOptions> action) => SwallowExceptionIfEntityExists(async () =>
            {
                if (!await adminClient.RuleExistsAsync(path, subscriptionName, ruleName))
                {
                    var options = new CreateRuleOptions(ruleName);
                    ProviderSettings.TopologyProvisioning?.CreateRuleOptions?.Invoke(options);
                    action?.Invoke(options);

                    logger.LogInformation("Creating rule: {RuleName} on subscription {SubscriptionName} on topic: {Path} ...", ruleName, subscriptionName, path);
                    await adminClient.CreateRuleAsync(path, subscriptionName, options);
                    return true;
                }
                return false;
            });

            var consumersSettingsByPath = Settings.Consumers.OfType<AbstractConsumerSettings>()
                .Concat(new[] { Settings.RequestResponse })
                .Where(x => x != null)
                .GroupBy(x => (x.Path, x.PathKind))
                .ToDictionary(x => x.Key, x => x.ToList());

            foreach (var ((path, pathKind), consumerSettingsList) in consumersSettingsByPath)
            {
                if (pathKind == PathKind.Queue && ProviderSettings.TopologyProvisioning.CanConsumerCreateQueue)
                {
                    await TryCreateQueue(path, options =>
                    {
                        foreach (var consumerSettings in consumerSettingsList)
                        {
                            // Note: Populate the require session flag on the queue
                            options.RequiresSession = consumerSettings.GetEnableSession();

                            consumerSettings.GetQueueOptions()?.Invoke(options);
                        }
                    });
                }
                if (pathKind == PathKind.Topic && ProviderSettings.TopologyProvisioning.CanConsumerCreateTopic)
                {
                    await TryCreateTopic(path, options =>
                    {
                        foreach (var consumerSettings in consumerSettingsList)
                        {
                            consumerSettings.GetTopicOptions()?.Invoke(options);
                        }
                    });

                    var consumerSettingsBySubscription = consumerSettingsList
                        .Select(x => (ConsumerSettings: x, SubscriptionName: x.GetSubscriptionName(required: false)))
                        .Where(x => x.SubscriptionName != null)
                        .ToDictionary(x => x.SubscriptionName, x => x.ConsumerSettings);

                    foreach (var (subscriptionName, consumerSettings) in consumerSettingsBySubscription)
                    {
                        var subscriptionCreated = await TryCreateSubscription(path, subscriptionName, options =>
                        {
                            // Note: Populate the require session flag on the subscription
                            options.RequiresSession = consumerSettings.GetEnableSession();

                            consumerSettings.GetSubscriptionOptions()?.Invoke(options);
                        });

                        var filters = consumerSettings.GetRules()?.Values;
                        if (filters != null && filters.Count > 0)
                        {
                            if (subscriptionCreated)
                            {
                                // Note: for a newly created subscription, ASB creates a default filter automatically, we need to remove it and let the user defined rules take over
                                await adminClient.DeleteRuleAsync(path, subscriptionName, "$Default");
                            }

                            var tasks = filters.Select(filter => TryCreateRule(path, subscriptionName, filter.Name, options =>
                            {
                                options.Filter = new SqlRuleFilter(filter.SqlFilter);
                                if (filter.SqlAction != null)
                                {
                                    options.Action = new SqlRuleAction(filter.SqlAction);
                                }
                            }));
                            await Task.WhenAll(tasks);
                        }
                    }
                }
            }

            foreach (var producerSettings in Settings.Producers)
            {
                if (producerSettings.PathKind == PathKind.Queue && ProviderSettings.TopologyProvisioning.CanProducerCreateQueue)
                {
                    await TryCreateQueue(producerSettings.DefaultPath, options => producerSettings.GetQueueOptions()?.Invoke(options));
                }
                if (producerSettings.PathKind == PathKind.Topic && ProviderSettings.TopologyProvisioning.CanProducerCreateTopic)
                {
                    await TryCreateTopic(producerSettings.DefaultPath, options => producerSettings.GetTopicOptions()?.Invoke(options));
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Could not provision Azure Service Bus topology");
        }
        finally
        {
            provisionTopologyTask = null;
            logger.LogInformation("Topology provisioning finished");
        }
    }

    protected override async Task OnStart()
    {
        await base.OnStart();
        await Task.WhenAll(consumers.Select(x => x.Start()));
    }

    protected override async Task OnStop()
    {
        await base.OnStop();
        await Task.WhenAll(consumers.Select(x => x.Stop()));
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore();

        if (consumers.Count > 0)
        {
            consumers.ForEach(c => c.DisposeSilently("Consumer", logger));
            consumers.Clear();
        }

        if (producerByPath.Dictonary.Count > 0)
        {
            await Task.WhenAll(producerByPath.Snapshot().Select(x =>
            {
                logger.LogDebug("Closing sender client for path {Path}", x.EntityPath);
                return x.CloseAsync();
            }));
            producerByPath.Clear();
        }

        if (client != null)
        {
            await client.DisposeAsync().ConfigureAwait(false);
            client = null;
        }
    }

    public override async Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, CancellationToken cancellationToken)
    {
        var messageType = message?.GetType();

        AssertActive();

        logger.LogDebug("Producing message {Message} of type {MessageType} to path {Path} with size {MessageSize}", message, messageType?.Name, path, messagePayload?.Length ?? 0);

        var m = messagePayload != null ? new ServiceBusMessage(messagePayload) : new ServiceBusMessage();

        // add headers
        if (messageHeaders != null)
        {
            foreach (var header in messageHeaders)
            {
                m.ApplicationProperties.Add(header.Key, header.Value);
            }
        }

        if (messageType != null)
        {
            var producerSettings = GetProducerSettings(messageType);

            // execute message modifier
            try
            {
                var messageModifier = producerSettings.GetMessageModifier();
                messageModifier?.Invoke(message, m);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "The configured message modifier failed for message type {MessageType} and message {Message}", messageType, message);
            }
        }

        var senderClient = producerByPath.GetOrAdd(path);

        try
        {
            var t = provisionTopologyTask;
            if (t != null)
            {
                // await until topology is provisioned for the first time
                await t;
            }

            await senderClient.SendMessageAsync(m, cancellationToken: cancellationToken).ConfigureAwait(false);

            logger.LogDebug("Delivered message {Message} of type {MessageType} to {Path}", message, messageType?.Name, path);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Producing message {Message} of type {MessageType} to path {Path} resulted in error {Error}", message, messageType?.Name, path, ex.Message);
            throw new PublishMessageBusException($"Producing message {message} of type {messageType?.Name} to path {path} resulted in error: {ex.Message}", ex);
        }
    }

    public override Task ProduceRequest(object request, IDictionary<string, object> headers, string path, ProducerSettings producerSettings)
    {
        if (headers is null) throw new ArgumentNullException(nameof(headers));

        return base.ProduceRequest(request, headers, path, producerSettings);
    }

    public override Task ProduceResponse(object request, IDictionary<string, object> requestHeaders, object response, IDictionary<string, object> responseHeaders, ConsumerSettings consumerSettings)
    {
        if (requestHeaders is null) throw new ArgumentNullException(nameof(requestHeaders));
        if (consumerSettings is null) throw new ArgumentNullException(nameof(consumerSettings));

        return base.ProduceResponse(consumerSettings.ResponseType, requestHeaders, response, responseHeaders, consumerSettings);
    }

    #endregion
}
