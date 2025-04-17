namespace SlimMessageBus.Host.AmazonSQS;

using Amazon.SimpleNotificationService.Model;

public class SqsTopologyService
{
    private readonly ILogger _logger;
    private readonly MessageBusSettings _settings;
    private readonly SqsMessageBusSettings _providerSettings;
    private readonly ISqsClientProvider _clientProviderSqs;
    private readonly ISnsClientProvider _clientProviderSns;

    public SqsTopologyService(
        ILogger<SqsTopologyService> logger,
        MessageBusSettings settings,
        SqsMessageBusSettings providerSettings,
        ISqsClientProvider clientProviderSqs,
        ISnsClientProvider clientProviderSns)
    {
        _logger = logger;
        _settings = settings;
        _providerSettings = providerSettings;
        _clientProviderSqs = clientProviderSqs;
        _clientProviderSns = clientProviderSns;
    }

    public Task ProvisionTopology(CancellationToken cancellationToken) => _providerSettings.TopologyProvisioning.OnProvisionTopology(_clientProviderSqs.Client, _clientProviderSns.Client, () => DoProvisionTopology(cancellationToken), cancellationToken);

    private async Task EnsureQueue(string path,
                                   bool fifo,
                                   int? visibilityTimeout,
                                   string policy,
                                   Dictionary<string, string> attributes,
                                   Dictionary<string, string> tags,
                                   Dictionary<string, string> urlByPath,
                                   bool canCreate,
                                   CancellationToken cancellationToken)
    {
        try
        {
            if (urlByPath.ContainsKey(path))
            {
                // already created
                return;
            }

            try
            {
                var queueUrl = await _clientProviderSqs.Client.GetQueueUrlAsync(path, cancellationToken);
                if (queueUrl != null)
                {
                    urlByPath[path] = queueUrl.QueueUrl;
                    return;
                }
            }
            catch (QueueDoesNotExistException)
            {
                // proceed to create the queue
            }

            if (!canCreate)
            {
                _logger.LogWarning("Cannot create queue {QueueName} as the provider does not allow it", path);
                return;
            }

            var createQueueRequest = new CreateQueueRequest
            {
                QueueName = path,
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
                urlByPath[path] = createQueueResponse.QueueUrl;
                _logger.LogInformation("Created queue {QueueName} with URL {QueueUrl}", path, createQueueResponse.QueueUrl);
            }
            catch (QueueNameExistsException)
            {
                _logger.LogInformation("Queue {QueueName} already exists", path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating queue {QueueName}", path);
        }
    }

    private async Task EnsureTopic(string path,
                                   Dictionary<string, string> attributes,
                                   Dictionary<string, string> tags,
                                   Dictionary<string, string> urlByPath,
                                   bool canCreate,
                                   CancellationToken cancellationToken)
    {
        try
        {
            if (urlByPath.ContainsKey(path))
            {
                // already created
                return;
            }

            try
            {
                var topicModel = await _clientProviderSns.Client.FindTopicAsync(path);
                if (topicModel != null)
                {
                    urlByPath[path] = topicModel.TopicArn;
                    return;
                }
            }
            catch (QueueDoesNotExistException)
            {
                // proceed to create the queue
            }

            if (!canCreate)
            {
                _logger.LogWarning("Cannot create topic {TopicName} as the provider does not allow it", path);
                return;
            }

            var createTopicRequest = new CreateTopicRequest
            {
                Name = path,
                Attributes = []
            };

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
                urlByPath[path] = createTopicResponse.TopicArn;
                _logger.LogInformation("Created topic {TopicName}", path);
            }
            catch (QueueNameExistsException)
            {
                _logger.LogInformation("Topic {TopicName} already exists", path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating topic {TopicName}", path);
        }
    }

    private async Task EnsureSubscription(string topic,
                                          string queue,
                                          string filterPolicy,
                                          Dictionary<string, string> urlByPath,
                                          bool canCreate,
                                          CancellationToken cancellationToken)
    {
        try
        {
            var topicUrl = urlByPath[topic];
            var queueUrl = urlByPath[queue];
            var subscriptions = await _clientProviderSns.Client.ListSubscriptionsByTopicAsync(topicUrl, cancellationToken);

            var subscription = subscriptions.Subscriptions.FirstOrDefault(x => x.Endpoint == queueUrl && x.Protocol == "sqs");
            if (subscription != null)
            {
                return; // it exists
            }

            if (!canCreate)
            {
                _logger.LogWarning("Cannot create subscription for topic {TopicName} and queue {QueueName} as the provider does not allow it", topic, queue);
                return;
            }

            var subscribeRequest = new SubscribeRequest
            {
                TopicArn = topicUrl,
                Protocol = "sqs",
                Endpoint = queueUrl,
                Attributes = []
            };

            if (!string.IsNullOrEmpty(filterPolicy))
            {
                subscribeRequest.Attributes["FilterPolicy"] = filterPolicy;
            }

            _providerSettings.TopologyProvisioning.CreateSubscriptionOptions?.Invoke(subscribeRequest);

            var createTopicResponse = await _clientProviderSns.Client.SubscribeAsync(subscribeRequest, cancellationToken);
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

            var urlByPath = new Dictionary<string, string>();

            foreach (var producer in _settings.Producers)
            {
                var attributes = producer.GetOrDefault(SqsProperties.Attributes, [])
                     .Concat(_settings.GetOrDefault(SqsProperties.Attributes, []))
                     .GroupBy(x => x.Key, x => x.Value)
                     .ToDictionary(x => x.Key, x => x.First());

                var tags = producer.GetOrDefault(SqsProperties.Tags, [])
                     .Concat(_settings.GetOrDefault(SqsProperties.Tags, []))
                     .GroupBy(x => x.Key, x => x.Value)
                     .ToDictionary(x => x.Key, x => x.First());

                if (producer.PathKind == PathKind.Queue)
                {
                    await EnsureQueue(
                        path: producer.DefaultPath,
                        fifo: producer.GetOrDefault(SqsProperties.EnableFifo, _settings, false),
                        visibilityTimeout: producer.GetOrDefault(SqsProperties.VisibilityTimeout, _settings, null),
                        policy: producer.GetOrDefault(SqsProperties.Policy, _settings, null),
                        attributes: attributes,
                        tags: tags,
                        urlByPath,
                        _providerSettings.TopologyProvisioning.CanProducerCreateQueue,
                        cancellationToken);

                }
                else
                {
                    await EnsureTopic(
                        path: producer.DefaultPath,
                        attributes: attributes,
                        tags: tags,
                        urlByPath,
                        _providerSettings.TopologyProvisioning.CanProducerCreateTopic,
                        cancellationToken);
                }
            }

            var consumersSettingsByPath = _settings
                .Consumers
                .OfType<AbstractConsumerSettings>()
                .Concat([_settings.RequestResponse])
                .Where(x => x != null)
                .GroupBy(x => (x.Path, x.PathKind))
                .ToDictionary(x => x.Key, x => x.Single());

            foreach (var ((path, pathKind), consumerSettings) in consumersSettingsByPath)
            {
                if (pathKind == PathKind.Queue)
                {
                    await EnsureQueue(
                        path: path,
                        fifo: consumerSettings.GetOrDefault(SqsProperties.EnableFifo, _settings, false),
                        visibilityTimeout: consumerSettings.GetOrDefault(SqsProperties.VisibilityTimeout, _settings, null),
                        policy: consumerSettings.GetOrDefault(SqsProperties.Policy, _settings, null),
                        attributes: [],
                        tags: [],
                        urlByPath,
                        _providerSettings.TopologyProvisioning.CanConsumerCreateQueue,
                        cancellationToken);

                    var subscribeToTopic = consumerSettings.GetOrDefault(SqsProperties.SubscribeToTopic, _settings);
                    if (subscribeToTopic != null)
                    {
                        await EnsureTopic(
                            path: subscribeToTopic,
                            attributes: [],
                            tags: [],
                            urlByPath,
                            _providerSettings.TopologyProvisioning.CanConsumerCreateTopic,
                            cancellationToken);

                        await EnsureSubscription(
                            topic: subscribeToTopic,
                            queue: path,
                            filterPolicy: consumerSettings.GetOrDefault(SqsProperties.SubscribeToTopicFilterPolicy, _settings),
                            urlByPath,
                            _providerSettings.TopologyProvisioning.CanConsumerCreateTopicSubscription,
                            cancellationToken);
                    }
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
