namespace SlimMessageBus.Host.AmazonSQS;

using Newtonsoft.Json;

internal class SqsTopologyService
{
    private readonly ILogger _logger;
    private readonly MessageBusSettings _settings;
    private readonly SqsMessageBusSettings _providerSettings;
    private readonly ISqsClientProvider _clientProviderSqs;
    private readonly ISnsClientProvider _clientProviderSns;
    private readonly ISqsTopologyCache _cache;

    public SqsTopologyService(
        ILogger<SqsTopologyService> logger,
        MessageBusSettings settings,
        SqsMessageBusSettings providerSettings,
        ISqsClientProvider clientProviderSqs,
        ISnsClientProvider clientProviderSns,
        ISqsTopologyCache cache)
    {
        _logger = logger;
        _settings = settings;
        _providerSettings = providerSettings;
        _clientProviderSqs = clientProviderSqs;
        _clientProviderSns = clientProviderSns;
        _cache = cache;
    }

    public Task ProvisionTopology(CancellationToken cancellationToken) => _providerSettings.TopologyProvisioning.OnProvisionTopology(_clientProviderSqs.Client, _clientProviderSns.Client, () => DoProvisionTopology(cancellationToken), cancellationToken);

    private static string GetQueuePolicyForTopic(string queueArn, string topicArn)
    {
        // Define the policy allowing SNS to send messages to SQS
        var policy = new
        {
            Version = "2012-10-17",
            Statement = new[]
            {
                new
                {
                    Sid = "Allow-SNS-SendMessage",
                    Effect = "Allow",
                    Principal = new { Service = "sns.amazonaws.com" },
                    Action = "sqs:SendMessage",
                    Resource = queueArn,
                    Condition = new
                    {
                        ArnEquals = new Dictionary<string, string>
                        {
                            {"aws:SourceArn", topicArn}
                        }
                    }
                }
            }
        };

        // Serialize policy to JSON
        // Note: Dependncy on Newtonsoft.Json, but its used by the SQS client plugin already.
        return JsonConvert.SerializeObject(policy);
    }

    private async Task EnsureQueue(string queue,
                                   bool fifo,
                                   int? visibilityTimeout,
                                   string policy,
                                   Dictionary<string, string> attributes,
                                   Dictionary<string, string> tags,
                                   bool canCreate,
                                   CancellationToken cancellationToken)
    {
        try
        {
            if (_cache.Contains(queue))
            {
                // already created
                return;
            }

            var queueMeta = await _cache.LookupQueue(queue, cancellationToken);
            if (queueMeta != null)
            {
                _cache.SetMeta(queue, queueMeta);
                return;
            }

            if (!canCreate)
            {
                _logger.LogWarning("Cannot create queue {QueueName} as the provider does not allow it", queue);
                return;
            }

            var createQueueRequest = new CreateQueueRequest
            {
                QueueName = queue,
                Attributes = []
            };

            if (fifo)
            {
                createQueueRequest.Attributes.Add(QueueAttributeName.FifoQueue, "true");
            }

            if (visibilityTimeout != null)
            {
                createQueueRequest.Attributes.Add(QueueAttributeName.VisibilityTimeout, visibilityTimeout.ToString());
            }

            if (policy != null)
            {
                createQueueRequest.Attributes.Add(QueueAttributeName.Policy, policy);
            }

            if (attributes.Count > 0)
            {
                createQueueRequest.Attributes = attributes;
            }

            if (tags.Count > 0)
            {
                createQueueRequest.Tags = tags;
            }

            _providerSettings.TopologyProvisioning.CreateQueueOptions?.Invoke(createQueueRequest);

            try
            {
                var createQueueResponse = await _clientProviderSqs.Client.CreateQueueAsync(createQueueRequest, cancellationToken);
                var getQueueAttributesResponse = await _clientProviderSqs.Client.GetQueueAttributesAsync(createQueueResponse.QueueUrl, [QueueAttributeName.QueueArn], cancellationToken);

                var queueArn = getQueueAttributesResponse.QueueARN;
                _cache.SetMeta(queue, new(url: createQueueResponse.QueueUrl, arn: queueArn, PathKind.Queue));

                _logger.LogInformation("Created queue {QueueName} with ARN {QueueArn}", queue, queueArn);
            }
            catch (QueueNameExistsException)
            {
                _logger.LogInformation("Queue {QueueName} already exists", queue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating queue {QueueName}", queue);
        }
    }

    private async Task EnsureTopic(string topic,
                                   bool fifo,
                                   Dictionary<string, string> attributes,
                                   Dictionary<string, string> tags,
                                   bool canCreate,
                                   CancellationToken cancellationToken)
    {
        try
        {
            if (_cache.Contains(topic))
            {
                // already created
                return;
            }

            var topicMeta = await _cache.LookupTopic(topic, cancellationToken);
            if (topicMeta != null)
            {
                _cache.SetMeta(topic, topicMeta);
                return;
            }

            if (!canCreate)
            {
                _logger.LogWarning("Cannot create topic {TopicName} as the provider does not allow it", topic);
                return;
            }

            var createTopicRequest = new CreateTopicRequest
            {
                Name = topic,
                Attributes = [],
            };

            if (fifo)
            {
                createTopicRequest.Attributes.Add("FifoTopic", "true");
            }

            if (attributes.Count > 0)
            {
                createTopicRequest.Attributes = attributes;
            }

            if (tags.Count > 0)
            {
                createTopicRequest.Tags = [.. tags.Select(x => new Tag { Key = x.Key, Value = x.Value })];
            }

            _providerSettings.TopologyProvisioning.CreateTopicOptions?.Invoke(createTopicRequest);

            try
            {
                var createTopicResponse = await _clientProviderSns.Client.CreateTopicAsync(createTopicRequest, cancellationToken);
                _cache.SetMeta(topic, new(url: null, arn: createTopicResponse.TopicArn, PathKind.Topic));
                _logger.LogInformation("Created topic {TopicName}", topic);
            }
            catch (QueueNameExistsException)
            {
                _logger.LogInformation("Topic {TopicName} already exists", topic);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating topic {TopicName}", topic);
        }
    }

    private async Task EnsureSubscription(string topic,
                                          string queue,
                                          string filterPolicy,
                                          bool canCreate,
                                          CancellationToken cancellationToken)
    {
        try
        {
            var topicMeta = await _cache.GetMetaWithPreloadOrException(topic, PathKind.Topic, cancellationToken);
            var queueMeta = await _cache.GetMetaWithPreloadOrException(queue, PathKind.Queue, cancellationToken);

            var subscriptions = await _clientProviderSns.Client.ListSubscriptionsByTopicAsync(topicMeta.Arn, cancellationToken);

            var subscription = subscriptions.Subscriptions.FirstOrDefault(x => x.Endpoint == queueMeta.Arn && x.Protocol == "sqs");
            if (subscription != null)
            {
                return; // it exists
            }

            if (!canCreate)
            {
                _logger.LogWarning("Cannot create subscription for topic {TopicName} and queue {QueueName} as the provider does not allow it", topic, queue);
                return;
            }

            // Ensure the policy is set on the queue to accept messages from the topic
            var policy = GetQueuePolicyForTopic(queueMeta.Arn, topicMeta.Arn);

            await _clientProviderSqs.Client.SetQueueAttributesAsync(
                queueUrl: queueMeta.Url,
                attributes: new Dictionary<string, string>
                {
                    [QueueAttributeName.Policy] = policy
                },
                cancellationToken);

            _logger.LogInformation("Set policy for queue {QueueName} to accept messages from topic {TopicName}, policy {Policy}", queue, topic, policy);

            var subscribeRequest = new SubscribeRequest
            {
                TopicArn = topicMeta.Arn,
                Protocol = "sqs",
                Endpoint = queueMeta.Arn,
                Attributes = []
            };

            if (!string.IsNullOrEmpty(filterPolicy))
            {
                subscribeRequest.Attributes["FilterPolicy"] = filterPolicy;
            }

            _providerSettings.TopologyProvisioning.CreateSubscriptionOptions?.Invoke(subscribeRequest);

            await _clientProviderSns.Client.SubscribeAsync(subscribeRequest, cancellationToken);
            _logger.LogInformation("Created subscription for topic {TopicName} and queue {QueueName}", topic, queue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subscription for topic {TopicName} and queue {QueueName}", topic, queue);
        }
    }

    private async Task DoProvisionTopology(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Topology provisioning started...");

            foreach (var producerSettings in _settings.Producers)
            {
                var attributes = producerSettings.GetOrDefault(SqsProperties.Attributes, [])
                     .Concat(_settings.GetOrDefault(SqsProperties.Attributes, []))
                     .GroupBy(x => x.Key, x => x.Value)
                     .ToDictionary(x => x.Key, x => x.First());

                var tags = producerSettings.GetOrDefault(SqsProperties.Tags, [])
                     .Concat(_settings.GetOrDefault(SqsProperties.Tags, []))
                     .GroupBy(x => x.Key, x => x.Value)
                     .ToDictionary(x => x.Key, x => x.First());

                var fifo = producerSettings.GetOrDefault(SqsProperties.EnableFifo, _settings, false);

                if (producerSettings.PathKind == PathKind.Queue)
                {
                    await EnsureQueue(
                        queue: producerSettings.DefaultPath,
                        fifo: fifo,
                        visibilityTimeout: producerSettings.GetOrDefault(SqsProperties.VisibilityTimeout, _settings, null),
                        policy: producerSettings.GetOrDefault(SqsProperties.Policy, _settings, null),
                        attributes: attributes,
                        tags: tags,
                        _providerSettings.TopologyProvisioning.CanProducerCreateQueue,
                        cancellationToken);
                }
                else
                {
                    await EnsureTopic(
                        topic: producerSettings.DefaultPath,
                        fifo: fifo,
                        attributes: attributes,
                        tags: tags,
                        _providerSettings.TopologyProvisioning.CanProducerCreateTopic,
                        cancellationToken);
                }
            }

            var consumersSettingsByPath = _settings
                .Consumers
                .OfType<AbstractConsumerSettings>()
                .Concat([_settings.RequestResponse])
                .Where(x => x != null)
                .GroupBy(x => x.GetOrDefault(SqsProperties.UnderlyingQueue))
                .Where(x => x.Key != null)
                .ToDictionary(x => x.Key, x => x.Single());

            foreach (var (queue, consumerSettings) in consumersSettingsByPath)
            {
                var attributes = consumerSettings.GetOrDefault(SqsProperties.Attributes, [])
                     .Concat(_settings.GetOrDefault(SqsProperties.Attributes, []))
                     .GroupBy(x => x.Key, x => x.Value)
                     .ToDictionary(x => x.Key, x => x.First());

                var tags = consumerSettings.GetOrDefault(SqsProperties.Tags, [])
                     .Concat(_settings.GetOrDefault(SqsProperties.Tags, []))
                     .GroupBy(x => x.Key, x => x.Value)
                     .ToDictionary(x => x.Key, x => x.First());

                var fifo = consumerSettings.GetOrDefault(SqsProperties.EnableFifo, _settings, false);

                await EnsureQueue(
                    queue: queue,
                    fifo: fifo,
                    visibilityTimeout: consumerSettings.GetOrDefault(SqsProperties.VisibilityTimeout, _settings, null),
                    policy: consumerSettings.GetOrDefault(SqsProperties.Policy, _settings, null),
                    attributes: attributes,
                    tags: tags,
                    _providerSettings.TopologyProvisioning.CanConsumerCreateQueue,
                    cancellationToken);

                var subscribeToTopic = consumerSettings.GetOrDefault(SqsProperties.SubscribeToTopic, _settings);
                if (subscribeToTopic != null)
                {
                    await EnsureTopic(
                        topic: subscribeToTopic,
                        fifo: fifo, // typically when the queue is FIFO, the topic is also FIFO
                        attributes: [],
                        tags: [],
                        _providerSettings.TopologyProvisioning.CanConsumerCreateTopic,
                        cancellationToken);

                    await EnsureSubscription(
                        topic: subscribeToTopic,
                        queue: queue,
                        filterPolicy: consumerSettings.GetOrDefault(SqsProperties.SubscribeToTopicFilterPolicy, _settings),
                        _providerSettings.TopologyProvisioning.CanConsumerCreateTopicSubscription,
                        cancellationToken);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not provision Amazon SQS/SNS topology");
        }
        finally
        {
            _logger.LogInformation("Topology provisioning finished");
        }
    }
}
