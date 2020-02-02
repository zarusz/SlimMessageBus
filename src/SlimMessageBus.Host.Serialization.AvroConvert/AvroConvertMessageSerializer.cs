using Common.Logging;
using SolTechnology.Avro.Codec;
using System;
using System.Globalization;
using System.Reflection;

namespace SlimMessageBus.Host.Serialization.AvroConvert
{
    /// <summary>
    /// AvroConvert library serialization implementation of <see cref="IMessageSerializer"/>
    /// </summary>
    public class AvroConvertMessageSerializer : IMessageSerializer
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly CodecType _codecType;

        public AvroConvertMessageSerializer(CodecType codecType)
        {
            _codecType = codecType;
        }

        public AvroConvertMessageSerializer()
            : this(CodecType.Null)
        {
        }

        #region Implementation of IMessageSerializer

        public byte[] Serialize(Type t, object message)
        {
            var payload = SolTechnology.Avro.AvroConvert.Serialize(message);
            Log.DebugFormat(CultureInfo.InvariantCulture, "Type {0} serialized from {1} to Avro bytes (size {2})", t, message, payload.Length);
            return payload;
        }

        public object Deserialize(Type t, byte[] payload)
        {
            var message = SolTechnology.Avro.AvroConvert.Deserialize(payload, t);
            Log.DebugFormat(CultureInfo.InvariantCulture, "Type {0} deserialized from Avro bytes (size {2}) to {1}", t, message, payload.Length);
            return message;
        }

        #endregion
    }
}
