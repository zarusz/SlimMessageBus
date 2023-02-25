namespace SlimMessageBus.Host;

/// <summary>
/// Special type that represents no value (or indicates there is no response type in request-response).
/// </summary>
public sealed class Void
{
    private Void()
    {
    }

    public static readonly Void Instance = new();
}