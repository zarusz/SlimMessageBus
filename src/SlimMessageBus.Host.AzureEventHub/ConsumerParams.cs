namespace SlimMessageBus.Host.AzureEventHub;

using Azure.Storage.Blobs;

public class ConsumerParams : PathGroup
{
    public BlobContainerClient CheckpointClient { get; set; }

    public ConsumerParams(string path, string group, BlobContainerClient checkpointClient) : base(path, group) 
        => CheckpointClient = checkpointClient;
}