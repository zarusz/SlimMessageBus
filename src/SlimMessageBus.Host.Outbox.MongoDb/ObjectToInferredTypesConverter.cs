namespace SlimMessageBus.Host.Outbox.MongoDb;

/// <summary>
/// Converter that infers object to primitive types during JSON deserialization.
/// See https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/converters-how-to?pivots=dotnet-7-0#deserialize-inferred-types-to-object-properties
/// </summary>
internal sealed class ObjectToInferredTypesConverter : JsonConverter<object>
{
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.True) return true;
        if (reader.TokenType == JsonTokenType.False) return false;
        if (reader.TokenType == JsonTokenType.Number)
            return reader.TryGetInt64(out var l) ? (object)l : reader.GetDouble();
        if (reader.TokenType == JsonTokenType.String)
            return reader.TryGetDateTime(out var datetime) ? (object)datetime : reader.GetString()!;
        return JsonDocument.ParseValue(ref reader).RootElement.Clone();
    }

    public override void Write(Utf8JsonWriter writer, object objectToWrite, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, objectToWrite, objectToWrite.GetType(), options);
}
