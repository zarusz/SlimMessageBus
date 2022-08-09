namespace SlimMessageBus.Host.AzureEventHub;

public class PathGroupPartitionId : PathGroup
{
    public string PartitionId { get; }

    public PathGroupPartitionId(string path, string group, string partitionId)
        : base(path, group)
    {
        PartitionId = partitionId;
    }
}