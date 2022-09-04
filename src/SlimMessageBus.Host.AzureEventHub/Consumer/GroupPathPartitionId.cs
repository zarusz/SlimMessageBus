namespace SlimMessageBus.Host.AzureEventHub;

public class GroupPathPartitionId : GroupPath
{
    public string PartitionId { get; }

    public GroupPathPartitionId(string path, string group, string partitionId)
        : base(path, group)
    {
        PartitionId = partitionId;
    }

    public GroupPathPartitionId(GroupPath groupPath, string partitionId)
        : base(path: groupPath.Path, group: groupPath.Group)
    {
        PartitionId = partitionId;
    }

    public override string ToString()
        => $"{Path}/{PartitionId}/{Group}";
}