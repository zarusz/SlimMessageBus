namespace SlimMessageBus.Host.Serialization.Avro;

using global::Avro;

public interface ISchemaLookupStrategy
{
    Schema Lookup(Type type);
}
