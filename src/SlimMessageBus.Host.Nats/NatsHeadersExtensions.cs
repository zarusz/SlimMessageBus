namespace SlimMessageBus.Host.Nats;

static internal class NatsHeadersExtensions
{
    public static IReadOnlyDictionary<string, object> ToReadOnlyDictionary(this NatsHeaders headers) =>
        headers == null ? new Dictionary<string, object>() : headers.ToDictionary(kvp => kvp.Key, kvp => (object) kvp.Value.ToString());
}