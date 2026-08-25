namespace SlimMessageBus.Host.GooglePubSub;

/// <summary>
/// Converts SlimMessageBus header values to and from Pub/Sub string attributes.
/// </summary>
public interface IGooglePubSubHeaderSerializer
{
    string Serialize(string key, object value);
    object Deserialize(string key, string value);
}
