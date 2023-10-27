namespace SlimMessageBus.Host;

public interface IResponseConsumer
{
    Task<Exception> OnResponseArrived(byte[] responsePayload, string path, IReadOnlyDictionary<string, object> responseHeaders);
}