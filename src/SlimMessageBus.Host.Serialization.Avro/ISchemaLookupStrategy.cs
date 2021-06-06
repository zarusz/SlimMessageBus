namespace SlimMessageBus.Host.Serialization.Avro
{
    using global::Avro;
    using System;

    public interface ISchemaLookupStrategy
    {
        Schema Lookup(Type type);
    }
}
