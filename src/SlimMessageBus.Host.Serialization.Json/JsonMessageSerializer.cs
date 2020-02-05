using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using Common.Logging;
using Newtonsoft.Json;

namespace SlimMessageBus.Host.Serialization.Json
{
    public class JsonMessageSerializer : IMessageSerializer
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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
            var jsonPayload = string.Empty;
            try
            {
                jsonPayload = _encoding.GetString(payload);
                var message = JsonConvert.DeserializeObject(jsonPayload, t, _serializerSettings);
                Log.DebugFormat(CultureInfo.InvariantCulture, "Type {0} deserialized from JSON {2} to {1}", t, message, jsonPayload);
                return message;
            }
            catch (Exception e)
            {
                Log.ErrorFormat(CultureInfo.InvariantCulture, "Type {0} could not been deserialized, payload: {1}, JSON: {2}", e, t, Log.IsDebugEnabled ? Convert.ToBase64String(payload) : "(...)", jsonPayload);
                throw;
            }
        }

        #endregion
    }
}