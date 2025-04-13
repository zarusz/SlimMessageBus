namespace SlimMessageBus.Host.AmazonSQS;

using System.Threading;

using Amazon.SimpleNotificationService.Model;
using Amazon.SQS.Model;

using Newtonsoft.Json;

public class SqsTopologyService
{
    private readonly ILogger _logger;
    private readonly MessageBusSettings _settings;
    private readonly SqsMessageBusSettings _providerSettings;
    private readonly ISqsClientProvider _clientProviderSqs;
    private readonly ISnsClientProvider _clientProviderSns;
    private readonly SqsTopologyCache _cache;

    public SqsTopologyService(
        ILogger<SqsTopologyService> logger,
        MessageBusSettings settings,
        SqsMessageBusSettings providerSettings,
        ISqsClientProvider clientProviderSqs,
        ISnsClientProvider clientProviderSns,
        SqsTopologyCache cache)
    {
        _logger = logger;
        _settings = settings;
        _providerSettings = providerSettings;
        _clientProviderSqs = clientProviderSqs;
        _clientProviderSns = clientProviderSns;
        _cache = cache;
    }

    public Task ProvisionTopology(CancellationToken cancellationToken) => _providerSettings.TopologyProvisioning.OnProvisionTopology(_clientProviderSqs.Client, _clientProviderSns.Client, () => DoProvisionTopology(cancellationToken), cancellationToken);

    private async Task<string> GetQueueArn(string queueUrl, CancellationToken cancellationToken)
    {
        var r = await _clientProviderSqs.Client.GetQueueAttributesAsync(queueUrl, [QueueAttributeName.QueueArn], cancellationToken);
        return r.QueueARN;
    }

    private string GetQueuePolicyForTopic(string queueArn, string topicArn)
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
        return JsonConvert.SerializeObject(policy);
    }

    private async Task EnsureQueue(string path,
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
            if (_cache.Contains(path))
            {
                // already created
                return;
            }

            try
            {
                var r = await _clientProviderSqs.Client.GetQueueUrlAsync(path, cancellationToken);
                if (r != null)
                {
                    var arn = await GetQueueArn(r.QueueUrl, cancellationToken);
                    _cache.SetMeta(path, new(url: r.QueueUrl, arn: arn, PathKind.Queue));
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
                var queueArn = await GetQueueArn(createQueueResponse.QueueUrl, cancellationToken);
                _cache.SetMeta(path, new(url: createQueueResponse.QueueUrl, arn: queueArn, PathKind.Queue));
                _logger.LogInformation("Created queue {QueueName} with ARN {QueueArn}", path, queueArn);
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
                                   bool fifo,
                                   Dictionary<string, string> attributes,
                                   Dictionary<string, string> tags,
                                   bool canCreate,
                                   CancellationToken cancellationToken)
    {
        try
        {
            if (_cache.Contains(path))
            {
                // already created
                return;
            }

            try
            {
                var topicModel = await _clientProviderSns.Client.FindTopicAsync(path);
                if (topicModel != null)
                {
                    _cache.SetMeta(path, new(url: null, arn: topicModel.TopicArn, PathKind.Topic));
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
                _cache.SetMeta(path, new(url: null, arn: createTopicResponse.TopicArn, PathKind.Topic));
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
                                          bool canCreate,
                                          CancellationToken cancellationToken)
    {
        try
        {
            var topicArn = _cache.GetMetaOrException(topic).Arn;
            var queueMeta = _cache.GetMetaOrException(queue);

            var subscriptions = await _clientProviderSns.Client.ListSubscriptionsByTopicAsync(topicArn, cancellationToken);

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
            var policy = GetQueuePolicyForTopic(queueMeta.Arn, topicArn);
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
                TopicArn = topicArn,
                Protocol = "sqs",
                Endpoint = queueMeta.Arn,
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
                        path: producerSettings.DefaultPath,
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
                        path: producerSettings.DefaultPath,
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
                .GroupBy(x => (x.Path, x.PathKind))
                .ToDictionary(x => x.Key, x => x.Single());

            foreach (var ((path, pathKind), consumerSettings) in consumersSettingsByPath)
            {
                var attributes = consumerSettings.GetOrDefault(SqsProperties.Attributes, [])
                     .Concat(_settings.GetOrDefault(SqsProperties.Attributes, []))
                     .GroupBy(x => x.Key, x => x.Value)
                     .ToDictionary(x => x.Key, x => x.First());

                var tags = consumerSettings.GetOrDefault(SqsProperties.Tags, [])
                     .Concat(_settings.GetOrDefault(SqsProperties.Tags, []))
                     .GroupBy(x => x.Key, x => x.Value)
                     .ToDictionary(x => x.Key, x => x.First());

                if (pathKind == PathKind.Queue)
                {
                    var fifo = consumerSettings.GetOrDefault(SqsProperties.EnableFifo, _settings, false);

                    await EnsureQueue(
                        path: path,
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
                            path: subscribeToTopic,
                            fifo: fifo, // typically when the queue is FIFO, the topic is also FIFO
                            attributes: [],
                            tags: [],
                            _providerSettings.TopologyProvisioning.CanConsumerCreateTopic,
                            cancellationToken);

                        await EnsureSubscription(
                            topic: subscribeToTopic,
                            queue: path,
                            filterPolicy: consumerSettings.GetOrDefault(SqsProperties.SubscribeToTopicFilterPolicy, _settings),
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
