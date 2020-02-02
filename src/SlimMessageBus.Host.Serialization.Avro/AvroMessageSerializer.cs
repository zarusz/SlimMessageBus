using Avro;
using Avro.IO;
using Avro.Specific;
using Common.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace SlimMessageBus.Host.Serialization.Avro
{
    /// <summary>
    /// Apache Avro serialization implementation of <see cref="IMessageSerializer"/>
    /// </summary>
    public class AvroMessageSerializer : IMessageSerializer
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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

        /// <summary>
        /// By default MessageFactory is set to use the <see cref="ReflectionMessageCreationStategy"/> strategy, WriteSchemaLookup and ReadSchemaLookup is set to use <see cref="ReflectionSchemaLookupStrategy"/>.
        /// </summary>
        public AvroMessageSerializer()
        {
            // Apply defaults
            WriteMemoryStreamFactory = () => new MemoryStream();
            ReadMemoryStreamFactory = (byte[] payload) => new MemoryStream(payload);

            var mf = new ReflectionMessageCreationStategy();
            var ml = new ReflectionSchemaLookupStrategy();

            MessageFactory = (Type type) => mf.Create(type);
            WriteSchemaLookup = (Type type) => ml.Lookup(type);
            ReadSchemaLookup = WriteSchemaLookup;
        }

        public AvroMessageSerializer(IMessageCreationStrategy messageCreationStrategy, ISchemaLookupStrategy writerAndReaderSchemaLookupStrategy)
            : this(messageCreationStrategy, writerAndReaderSchemaLookupStrategy, writerAndReaderSchemaLookupStrategy)
        {
        }

        public AvroMessageSerializer(IMessageCreationStrategy messageCreationStrategy, ISchemaLookupStrategy writerSchemaLookupStrategy, ISchemaLookupStrategy readerSchemaLookupStrategy)
            : this()
        {
            MessageFactory = (Type type) => messageCreationStrategy.Create(type);
            WriteSchemaLookup = (Type type) => writerSchemaLookupStrategy.Lookup(type);
            ReadSchemaLookup = (Type type) => readerSchemaLookupStrategy.Lookup(type);
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
