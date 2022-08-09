namespace SlimMessageBus.Host.AzureEventHub;

public class PathGroup
{
    public string Path { get; }
    public string Group { get; }

    public PathGroup(string path, string group)
    {
        Path = path;
        Group = group;
    }
}