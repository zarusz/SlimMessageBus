namespace SlimMessageBus.Host.Serialization.Json;

using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Newtonsoft.Json;

public class JsonMessageSerializer : IMessageSerializer, IMessageSerializer<string>
{
    private readonly ILogger _logger;
    private readonly Encoding _encoding;
    private readonly JsonSerializerSettings _serializerSettings;

    public JsonMessageSerializer(JsonSerializerSettings serializerSettings, Encoding encoding, ILogger<JsonMessageSerializer> logger)
    {
        _serializerSettings = serializerSettings;
        _encoding = encoding ?? Encoding.UTF8;
        _logger = logger;
    }

    public JsonMessageSerializer()
        : this(null, null, NullLogger<JsonMessageSerializer>.Instance)
    {
    }

    #region Implementation of IMessageSerializer

    public byte[] Serialize(Type t, object message)
    {
        var jsonPayload = JsonConvert.SerializeObject(message, t, _serializerSettings);
        _logger.LogDebug("Type {MessageType} serialized from {Message} to JSON {MessageJson}", t, message, jsonPayload);
        return _encoding.GetBytes(jsonPayload);
    }

    public object Deserialize(Type t, byte[] payload)
    {
        var jsonPayload = string.Empty;
        try
        {
            jsonPayload = _encoding.GetString(payload);
            return Deserialize(t, jsonPayload);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Type {MessageType} could not been deserialized, payload: {MessagePayload}, JSON: {MessageJson}", t, _logger.IsEnabled(LogLevel.Debug) ? Convert.ToBase64String(payload) : "(...)", jsonPayload);
            throw;
        }
    }

    #endregion

    #region Implementation of IMessageSerializer<string>

    string IMessageSerializer<string>.Serialize(Type t, object message)
    {
        var payload = JsonConvert.SerializeObject(message, t, _serializerSettings);
        _logger.LogDebug("Type {MessageType} serialized from {Message} to JSON {MessageJson}", t, message, payload);
        return payload;
    }

    public object Deserialize(Type t, string payload)
    {
        var message = JsonConvert.DeserializeObject(payload, t, _serializerSettings);
        _logger.LogDebug("Type {MessageType} deserialized from JSON {MessageJson} to {Message}", t, payload, message);
        return message;
    }

    #endregion
}