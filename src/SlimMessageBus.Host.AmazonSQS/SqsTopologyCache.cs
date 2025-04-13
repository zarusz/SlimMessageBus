namespace SlimMessageBus.Host.AmazonSQS;

public class SqsPathMeta
{
    public string Url { get; }
    public string Arn { get; }
    public PathKind PathKind { get; }
    public SqsPathMeta(string url, string arn, PathKind pathKind)
    {
        Url = url;
        Arn = arn;
        PathKind = pathKind;
    }
}

public class SqsTopologyCache
{
    private readonly Dictionary<string, SqsPathMeta> _metaByPath = [];

    internal SqsPathMeta GetMetaOrException(string path)
    {
        if (_metaByPath.TryGetValue(path, out var urlAndKind))
        {
            return urlAndKind;
        }
        throw new ProducerMessageBusException($"The {path} has unknown URL/ARN at this point. Ensure the queue exists in Amazon SQS/SNS and the queue or topic is declared in SMB.");
    }

    internal void SetMeta(string path, SqsPathMeta meta)
    {
        if (_metaByPath.TryGetValue(path, out var existingMeta))
        {
            if (existingMeta.PathKind != meta.PathKind)
            {
                throw new ConfigurationMessageBusException($"Path {path} is declared as both {existingMeta.PathKind} and {meta.PathKind}");
            }
        }
        else
        {
            _metaByPath[path] = meta;
        }
    }

    public bool Contains(string path) => _metaByPath.ContainsKey(path);
}