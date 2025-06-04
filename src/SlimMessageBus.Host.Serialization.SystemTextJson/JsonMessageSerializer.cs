namespace SlimMessageBus.Host.Serialization.SystemTextJson;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Implementation of <see cref="IMessageSerializer"/> using <see cref="JsonSerializer"/>.
/// </summary>
public class JsonMessageSerializer : IMessageSerializer, IMessageSerializer<string>, IMessageSerializerProvider
{
    /// <summary>
    /// <see cref="JsonSerializerOptions"/> options for the JSON serializer. By default adds <see cref="ObjectToInferredTypesConverter"/> converter.
    /// </summary>
    public JsonSerializerOptions Options { get; set; }

    public JsonMessageSerializer(JsonSerializerOptions options = null)
    {
        Options = options ?? CreateDefaultOptions();
    }

    public virtual JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            AllowTrailingCommas = true
        };
        options.Converters.Add(new ObjectToInferredTypesConverter());
        return options;
    }

    #region Implementation of IMessageSerializer

    public byte[] Serialize(Type messageType, IDictionary<string, object> headers, object message, object transportMessage)
        => JsonSerializer.SerializeToUtf8Bytes(message, messageType, Options);

    public object Deserialize(Type messageType, IReadOnlyDictionary<string, object> headers, byte[] payload, object transportMessage)
        => JsonSerializer.Deserialize(payload, messageType, Options)!;

    #endregion

    #region Implementation of IMessageSerializer<string>

    string IMessageSerializer<string>.Serialize(Type messageType, IDictionary<string, object> headers, object message, object transportMessage)
        => JsonSerializer.Serialize(message, messageType, Options);

    public object Deserialize(Type messageType, IReadOnlyDictionary<string, object> headers, string payload, object transportMessage)
        => JsonSerializer.Deserialize(payload, messageType, Options)!;

    #endregion

    #region Implementation of IMessageSerializerProvider

    public IMessageSerializer GetSerializer(string path) => this;

    #endregion
}
