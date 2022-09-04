namespace SlimMessageBus.Host.AzureEventHub;

public class GroupPath
{
    public string Path { get; }
    public string Group { get; }

    public GroupPath(string path, string group)
    {
        Path = path;
        Group = group;
    }

    public override string ToString()
        => $"{Path}/{Group}";
}