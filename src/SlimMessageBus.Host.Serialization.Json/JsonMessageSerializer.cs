using System;
using System.Globalization;
using System.Text;
using Common.Logging;
using Newtonsoft.Json;

namespace SlimMessageBus.Host.Serialization.Json
{
    public class JsonMessageSerializer : IMessageSerializer
    {
        private static readonly ILog Log = LogManager.GetLogger<JsonMessageSerializer>();

        private readonly Encoding _encoding;
        private readonly JsonSerializerSettings _serializerSettings;

        public JsonMessageSerializer(JsonSerializerSettings serializerSettings, Encoding encoding)
        {
            _serializerSettings = serializerSettings;
            _encoding = encoding;
        }

        public JsonMessageSerializer()
            : this(null, Encoding.UTF8)
        {
        }

        #region Implementation of IMessageSerializer

        public byte[] Serialize(Type t, object message)
        {
            var jsonPayload = JsonConvert.SerializeObject(message, t, _serializerSettings);
            Log.DebugFormat(CultureInfo.InvariantCulture, "Type {0} serialized from {1} to JSON {2}", t, message, jsonPayload);

            var payload = _encoding.GetBytes(jsonPayload);
            return payload;
        }

        public object Deserialize(Type t, byte[] payload)
        {
            var jsonPayload = _encoding.GetString(payload);
            var message = JsonConvert.DeserializeObject(jsonPayload, t, _serializerSettings);
            Log.DebugFormat(CultureInfo.InvariantCulture, "Type {0} deserialized from JSON {2} to {1}", t, message, jsonPayload);
            return message;
        }

        #endregion
    }
}