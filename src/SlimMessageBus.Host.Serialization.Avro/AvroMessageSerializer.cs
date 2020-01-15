using Avro;
using Avro.IO;
using Avro.Specific;
using Common.Logging;
using System;
using System.Globalization;
using System.IO;

namespace SlimMessageBus.Host.Serialization.Avro
{
    /// <summary>
    /// Apache Avro serialization implementation of <see cref="IMessageSerializer"/>
    /// </summary>
    public class AvroMessageSerializer : IMessageSerializer
    {
        private static readonly ILog Log = LogManager.GetLogger<AvroMessageSerializer>();

        /// <summary>
        /// Allows to customize how are the <see cref="MemoryStream"/>s created and potentially introduce a strategy to reuse them.
        /// </summary>
        public Func<MemoryStream> WriteMemoryStreamFactory { get; set; }

        /// <summary>
        /// Allows to customize how are the <see cref="MemoryStream"/>s created and potentially introduce a strategy to reuse them.
        /// </summary>
        public Func<byte[], MemoryStream> ReadMemoryStreamFactory { get; set; }

        /// <summary>
        /// Allows to customize message creation of a given type. This is used while deserializing a message.
        /// </summary>
        public Func<Type, object> MessageFactory { get; set; }

        /// <summary>
        /// Used to look up a <see cref="Schema"/> for writing a message of type <see cref="Type"/>.
        /// </summary>
        public Func<Type, Schema> WriteSchemaLookup { get; set; }

        /// <summary>
        /// Used to look up a <see cref="Schema"/> for reading a message of type <see cref="Type"/>.
        /// </summary>
        public Func<Type, Schema> ReadSchemaLookup { get; set; }

        public AvroMessageSerializer()
        {
            // Apply defaults
            WriteMemoryStreamFactory = () => new MemoryStream();
            ReadMemoryStreamFactory = (byte[] payload) => new MemoryStream(payload);
            MessageFactory = (Type type) =>
            {
                try
                {
                    // by default create types using reflection
                    Log.DebugFormat(CultureInfo.InvariantCulture, "Instantiating type {0}", type);
                    var constructor = type.GetConstructor(Type.EmptyTypes);
                    return constructor.Invoke(null);
                }
                catch (Exception e)
                {
                    Log.ErrorFormat(CultureInfo.InvariantCulture, "Error intantiating message type {0}. Ensure it has a public and paremeterless constructor", e, type);
                    throw;
                }
            };
            WriteSchemaLookup = (Type type) => null;
            ReadSchemaLookup = (Type type) => null;
        }

        public object Deserialize(Type t, byte[] payload)
        {
            using (var ms = ReadMemoryStreamFactory(payload))
            {
                var dec = new BinaryDecoder(ms);

                var message = MessageFactory(t);

                var readerSchema = ReadSchemaLookup(t);
                AssertSchemaNotNull(t, readerSchema, false);

                var writerSchema = WriteSchemaLookup(t);
                AssertSchemaNotNull(t, writerSchema, true);

                Log.DebugFormat(CultureInfo.InvariantCulture, "Type {0} writer schema: {1}, reader schema: {2}", t, writerSchema, readerSchema);

                var reader = new SpecificDefaultReader(writerSchema, readerSchema);
                reader.Read(message, dec);
                return message;
            }
        }

        private static void AssertSchemaNotNull(Type t, Schema schema, bool writerSchema)
        {
            if (schema == null)
            {
                var role = writerSchema ? "Writer" : "Reader";
                throw new ArgumentNullException(nameof(schema), $"{role} schema lookup for type {t} returned null");
            }
        }

        public byte[] Serialize(Type t, object message)
        {
            using (var ms = WriteMemoryStreamFactory())
            {
                var enc = new BinaryEncoder(ms);

                var writerSchema = WriteSchemaLookup(t);
                AssertSchemaNotNull(t, writerSchema, true);

                Log.DebugFormat(CultureInfo.InvariantCulture, "Type {0} writer schema: {1}", t, writerSchema);

                var writer = new SpecificDefaultWriter(writerSchema); // Schema comes from pre-compiled, code-gen phase
                writer.Write(message, enc);
                return ms.ToArray();
            }
        }
    }
}
