using Avro;
using System;

namespace SlimMessageBus.Host.Serialization.Avro
{
    public interface ISchemaLookupStrategy
    {
        Schema Lookup(Type type);
    }
}
