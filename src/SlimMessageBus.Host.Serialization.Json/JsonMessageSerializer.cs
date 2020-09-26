using System;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;

namespace SlimMessageBus.Host.Serialization.Json
{
    public class JsonMessageSerializer : IMessageSerializer
    {
        private readonly ILogger _logger;
        private readonly Encoding _encoding;
        private readonly JsonSerializerSettings _serializerSettings;

        public JsonMessageSerializer(JsonSerializerSettings serializerSettings, Encoding encoding, ILogger<JsonMessageSerializer> logger)
        {
            _serializerSettings = serializerSettings;
            _encoding = encoding;
            _logger = logger;
        }

        public JsonMessageSerializer()
            : this(null, Encoding.UTF8, NullLogger<JsonMessageSerializer>.Instance)
        {
        }

        #region Implementation of IMessageSerializer

        public byte[] Serialize(Type t, object message)
        {
            var jsonPayload = JsonConvert.SerializeObject(message, t, _serializerSettings);
            _logger.LogDebug("Type {0} serialized from {1} to JSON {2}", t, message, jsonPayload);

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
                _logger.LogDebug("Type {0} deserialized from JSON {2} to {1}", t, message, jsonPayload);
                return message;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Type {0} could not been deserialized, payload: {1}, JSON: {2}", t, _logger.IsEnabled(LogLevel.Debug) ? Convert.ToBase64String(payload) : "(...)", jsonPayload);
                throw;
            }
        }

        #endregion
    }
}