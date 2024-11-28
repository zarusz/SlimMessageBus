namespace SlimMessageBus.Host.AmazonSQS;
public class SqsTopologyService
{
    private readonly ILogger _logger;
    private readonly MessageBusSettings _settings;
    private readonly SqsMessageBusSettings _providerSettings;
    private readonly ISqsClientProvider _clientProvider;

    public SqsTopologyService(
        ILogger<SqsTopologyService> logger,
        MessageBusSettings settings,
        SqsMessageBusSettings providerSettings,
        ISqsClientProvider clientProvider)
    {
        _logger = logger;
        _settings = settings;
        _providerSettings = providerSettings;
        _clientProvider = clientProvider;
    }

    public Task ProvisionTopology() => _providerSettings.TopologyProvisioning.OnProvisionTopology(_clientProvider.Client, DoProvisionTopology);

    private async Task CreateQueue(string path,
                                   bool fifo,
                                   int? visibilityTimeout,
                                   string policy,
                                   Dictionary<string, string> attributes,
                                   Dictionary<string, string> tags)
    {
        try
        {
            try
            {
                var queueUrl = await _clientProvider.Client.GetQueueUrlAsync(path);
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
                var createQueueResponse = await _clientProvider.Client.CreateQueueAsync(createQueueRequest);
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

    private async Task DoProvisionTopology()
    {
        try
        {
            _logger.LogInformation("Topology provisioning started...");

            if (_providerSettings.TopologyProvisioning.CanConsumerCreateQueue)
            {
                var consumersSettingsByPath = _settings.Consumers
                    .OfType<AbstractConsumerSettings>()
                    .Concat([_settings.RequestResponse])
                    .Where(x => x != null)
                    .GroupBy(x => (x.Path, x.PathKind))
                    .ToDictionary(x => x.Key, x => x.ToList());

                foreach (var ((path, pathKind), consumerSettingsList) in consumersSettingsByPath)
                {
                    if (pathKind == PathKind.Queue)
                    {
                        await CreateQueue(
                            path: path,
                            fifo: consumerSettingsList.Any(cs => cs.GetOrDefault(SqsProperties.EnableFifo, _settings, false)),
                            visibilityTimeout: consumerSettingsList.Select(cs => cs.GetOrDefault(SqsProperties.VisibilityTimeout, _settings, null)).FirstOrDefault(x => x != null),
                            policy: consumerSettingsList.Select(cs => cs.GetOrDefault(SqsProperties.Policy, _settings, null)).FirstOrDefault(x => x != null),
                            attributes: [],
                            tags: []);
                    }
                }
            }

            if (_providerSettings.TopologyProvisioning.CanProducerCreateQueue)
            {
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
                        await CreateQueue(
                            path: producer.DefaultPath,
                            fifo: producer.GetOrDefault(SqsProperties.EnableFifo, _settings, false),
                            visibilityTimeout: producer.GetOrDefault(SqsProperties.VisibilityTimeout, _settings, null),
                            policy: producer.GetOrDefault(SqsProperties.Policy, _settings, null),
                            attributes: attributes,
                            tags: tags);
                    }
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
