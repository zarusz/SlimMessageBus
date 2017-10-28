using System;
using System.Text;
using Newtonsoft.Json;

namespace SlimMessageBus.Host.Serialization.Json
{
    public class JsonMessageSerializer : IMessageSerializer
    {
        #region Implementation of IMessageSerializer

        public byte[] Serialize(Type t, object message)
        {
            var jsonPayload = JsonConvert.SerializeObject(message);
            var payload = Encoding.UTF8.GetBytes(jsonPayload);
            return payload;
        }

        public object Deserialize(Type t, byte[] payload)
        {
            var jsonPayload = Encoding.UTF8.GetString(payload);
            var message = JsonConvert.DeserializeObject(jsonPayload, t);
            return message;
        }

        #endregion
    }
}