namespace SlimMessageBus.Host.AmazonSQS;

internal interface ISqsTopologyCache
{
    SqsPathMeta GetMetaOrException(string path);
    Task<SqsPathMeta> GetMetaWithPreloadOrException(string path, PathKind pathKind, CancellationToken cancellationToken);
    void SetMeta(string path, SqsPathMeta meta);
    bool Contains(string path);
    Task<SqsPathMeta> LookupQueue(string path, CancellationToken cancellationToken);
    Task<SqsPathMeta> LookupTopic(string path, CancellationToken cancellationToken);
}

internal class SqsTopologyCache(ISqsClientProvider clientProviderSqs, ISnsClientProvider clientProviderSns) : ISqsTopologyCache
{
    private Dictionary<string, SqsPathMeta> _metaByPath = [];
    private readonly SemaphoreSlim _metaByPathSemaphore = new(1, 1);

    public SqsPathMeta GetMetaOrException(string path)
    {
        if (_metaByPath.TryGetValue(path, out var urlAndKind))
        {
            return urlAndKind;
        }
        throw new ProducerMessageBusException($"The {path} has unknown URL/ARN at this point. Ensure the queue exists in Amazon SQS/SNS and the queue or topic is declared in SMB.");
    }

    /// <summary>
    /// Get the metadata for the given path, preloading it if necessary in a thread-safe manner.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="pathKind"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<SqsPathMeta> GetMetaWithPreloadOrException(string path, PathKind pathKind, CancellationToken cancellationToken)
    {
        // Try to first load it from cache first
        if (_metaByPath.TryGetValue(path, out var pathMeta))
        {
            return pathMeta;
        }

        // Avoid updating the cache while another thread is doing it
        await _metaByPathSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Check again if it has not been preloaded yet before the semaphore was acquired
            if (!_metaByPath.ContainsKey(path))
            {
                pathMeta = pathKind == PathKind.Queue
                    ? await LookupQueue(path, cancellationToken)
                    : await LookupTopic(path, cancellationToken);

                if (pathMeta != null)
                {
                    // Create a new dictionary to avoid modifying the original one while iterating
                    var newMetaByPath = new Dictionary<string, SqsPathMeta>(_metaByPath);
                    SetMetaInternal(path, pathMeta, newMetaByPath);

                    // Update the cache with the new metadata dictionary
                    _metaByPath = newMetaByPath;
                }
            }
        }
        finally
        {
            _metaByPathSemaphore.Release();
        }

        return GetMetaOrException(path);
    }

    private static void SetMetaInternal(string path, SqsPathMeta meta, IDictionary<string, SqsPathMeta> metaByPath)
    {
        if (metaByPath.TryGetValue(path, out var existingMeta))
        {
            if (existingMeta.PathKind != meta.PathKind)
            {
                throw new ConfigurationMessageBusException($"Path {path} is declared as both {existingMeta.PathKind} and {meta.PathKind}");
            }
        }
        else
        {
            metaByPath[path] = meta;
        }
    }

    public void SetMeta(string path, SqsPathMeta meta) => SetMetaInternal(path, meta, _metaByPath);

    public bool Contains(string path) => _metaByPath.ContainsKey(path);

    public async Task<SqsPathMeta> LookupQueue(string path, CancellationToken cancellationToken)
    {
        try
        {
            var getQueueUrlResponse = await clientProviderSqs.Client.GetQueueUrlAsync(path, cancellationToken);
            if (getQueueUrlResponse != null)
            {
                var getQueueAttributesResponse = await clientProviderSqs.Client.GetQueueAttributesAsync(getQueueUrlResponse.QueueUrl, [QueueAttributeName.QueueArn], cancellationToken);
                return new(url: getQueueUrlResponse.QueueUrl, arn: getQueueAttributesResponse.QueueARN, PathKind.Queue);
            }
        }
        catch (QueueDoesNotExistException)
        {
            // proceed to create the queue
        }
        return null;
    }

    public async Task<SqsPathMeta> LookupTopic(string path, CancellationToken cancellationToken)
    {
        try
        {
            var topicModel = await clientProviderSns.Client.FindTopicAsync(path);
            if (topicModel != null)
            {
                return new(url: null, arn: topicModel.TopicArn, PathKind.Topic);
            }
        }
        catch (QueueDoesNotExistException)
        {
            // proceed to create the queue
        }
        return null;
    }
}