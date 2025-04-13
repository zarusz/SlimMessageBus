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
                                   CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                var queueUrl = await _clientProviderSqs.Client.GetQueueUrlAsync(path, cancellationToken);
                if (queueUrl != null)
                {
                    return;
                }
            }
            catch (QueueDoesNotExistException)
            {
                // proceed to create the queue
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
                                   CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                var topicModel = await _clientProviderSns.Client.FindTopicAsync(path);
                if (topicModel != null)
                {
                    return;
                }
            }
            catch (QueueDoesNotExistException)
            {
                // proceed to create the queue
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


    private async Task DoProvisionTopology(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Topology provisioning started...");

            var consumersSettingsByPath = _settings
                .Consumers
                .OfType<AbstractConsumerSettings>()
                .Concat([_settings.RequestResponse])
                .Where(x => x != null)
                .GroupBy(x => (x.Path, x.PathKind))
                .ToDictionary(x => x.Key, x => x.ToList());

            foreach (var ((path, pathKind), consumerSettingsList) in consumersSettingsByPath)
            {
                if (pathKind == PathKind.Queue && _providerSettings.TopologyProvisioning.CanConsumerCreateQueue)
                {
                    await EnsureQueue(
                        path: path,
                        fifo: consumerSettingsList.Any(cs => cs.GetOrDefault(SqsProperties.EnableFifo, _settings, false)),
                        visibilityTimeout: consumerSettingsList.Select(cs => cs.GetOrDefault(SqsProperties.VisibilityTimeout, _settings, null)).FirstOrDefault(x => x != null),
                        policy: consumerSettingsList.Select(cs => cs.GetOrDefault(SqsProperties.Policy, _settings, null)).FirstOrDefault(x => x != null),
                        attributes: [],
                        tags: [],
                        cancellationToken);
                }
                if (pathKind == PathKind.Topic && _providerSettings.TopologyProvisioning.CanConsumerCreateTopic)
                {
                    await EnsureTopic(
                        path: path,
                        attributes: [],
                        tags: [],
                        cancellationToken);
                }
            }

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

                if (producer.PathKind == PathKind.Queue && _providerSettings.TopologyProvisioning.CanProducerCreateQueue)
                {
                    await EnsureQueue(
                        path: producer.DefaultPath,
                        fifo: producer.GetOrDefault(SqsProperties.EnableFifo, _settings, false),
                        visibilityTimeout: producer.GetOrDefault(SqsProperties.VisibilityTimeout, _settings, null),
                        policy: producer.GetOrDefault(SqsProperties.Policy, _settings, null),
                        attributes: attributes,
                        tags: tags,
                        cancellationToken);

                }
                if (producer.PathKind == PathKind.Topic && _providerSettings.TopologyProvisioning.CanProducerCreateTopic)
                {
                    await EnsureTopic(
                        path: producer.DefaultPath,
                        attributes: attributes,
                        tags: tags,
                        cancellationToken);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not provision Amazon SQS topology");
        }
        finally
        {
            _logger.LogInformation("Topology provisioning finished");
        }
    }
}
