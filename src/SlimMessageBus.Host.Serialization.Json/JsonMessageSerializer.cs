namespace SlimMessageBus.Host.Serialization.Json;

using System.Collections.Generic;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Newtonsoft.Json;

public partial class JsonMessageSerializer : IMessageSerializer, IMessageSerializer<string>, IMessageSerializerProvider
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

    public byte[] Serialize(Type messageType, IDictionary<string, object> headers, object message, object transportMessage)
    {
        var jsonPayload = JsonConvert.SerializeObject(message, messageType, _serializerSettings);
        LogSerialized(messageType, message, jsonPayload);
        return _encoding.GetBytes(jsonPayload);
    }

    public object Deserialize(Type messageType, IReadOnlyDictionary<string, object> headers, byte[] payload, object transportMessage)
    {
        var jsonPayload = string.Empty;
        try
        {
            jsonPayload = _encoding.GetString(payload);
            return Deserialize(messageType, headers, jsonPayload, transportMessage);
        }
        catch (Exception e)
        {
            var base64Payload = _logger.IsEnabled(LogLevel.Debug)
                ? Convert.ToBase64String(payload)
                : "(...)";

            LogDeserializationFailed(messageType, jsonPayload, base64Payload, e);
            throw;
        }
    }

    #endregion

    #region Implementation of IMessageSerializer<string>

    string IMessageSerializer<string>.Serialize(Type messageType, IDictionary<string, object> headers, object message, object transportMessage)
    {
        var payload = JsonConvert.SerializeObject(message, messageType, _serializerSettings);
        LogSerialized(messageType, message, payload);
        return payload;
    }

    public object Deserialize(Type messageType, IReadOnlyDictionary<string, object> headers, string payload, object transportMessage)
    {
        try
        {
            var message = JsonConvert.DeserializeObject(payload, messageType, _serializerSettings);
            LogDeserializedFromString(messageType, payload, message);
            return message;
        }
        catch (Exception e)
        {
            LogDeserializationFailed(messageType, payload, string.Empty, e);
            throw;
        }
    }

    #endregion

    #region Implementation of IMessageSerializerProvider

    public IMessageSerializer GetSerializer(string path) => this;

    #endregion

    #region Logging

#if !NETSTANDARD2_0

    [LoggerMessage(
       EventId = 0,
       Level = LogLevel.Debug,
       Message = "Type {MessageType} serialized from {Message} to JSON {MessageJson}")]
    private partial void LogSerialized(Type messageType, object message, string messageJson);

    [LoggerMessage(
       EventId = 1,
       Level = LogLevel.Debug,
       Message = "Type {MessageType} deserialized from JSON {MessageJson} to {Message}")]
    private partial void LogDeserializedFromString(Type messageType, string messageJson, object message);

    [LoggerMessage(
       EventId = 2,
       Level = LogLevel.Error,
       Message = "Type {MessageType} could not been deserialized, payload: {MessagePayload}, JSON: {MessageJson}")]
    private partial void LogDeserializationFailed(Type messageType, string messageJson, string messagePayload, Exception e);

#endif

    #endregion
}

#if NETSTANDARD2_0

public partial class JsonMessageSerializer
{
    private void LogSerialized(Type messageType, object message, string messageJson)
        => _logger.LogDebug("Type {MessageType} serialized from {Message} to JSON {MessageJson}", messageType, message, messageJson);

    private void LogDeserializedFromString(Type messageType, string messageJson, object message)
        => _logger.LogDebug("Type {MessageType} deserialized from JSON {MessageJson} to {Message}", messageType, messageJson, message);

    private void LogDeserializationFailed(Type messageType, string messageJson, string messagePayload, Exception e)
        => _logger.LogError(e, "Type {MessageType} could not been deserialized, payload: {MessagePayload}, JSON: {MessageJson}", messageType, messagePayload, messageJson);
}

#endif