using System;
using System.Text;

namespace SlimMessageBus.Host
{
    public class MessageWithHeadersSerializer : IMessageSerializer
    {
        private readonly Encoding _encoding;

        public MessageWithHeadersSerializer()
            : this(Encoding.ASCII)
        {            
        }

        public MessageWithHeadersSerializer(Encoding encoding)
        {
            _encoding = encoding;
        }


        protected byte[] Serialize(MessageWithHeaders message)
        {
            // calculate bytes needed

            // 1 byte header count
            var n = 1;
            foreach (var header in message.Headers)
            {
                // 1 byte for key length
                n += 1 + _encoding.GetByteCount(header.Key);
                // 1 byte for value length
                n += 1 + _encoding.GetByteCount(header.Value);
            }
            n += message.Payload.Length;


            // allocate bytes
            var payload = new byte[n];

            
            // write bytes
            var i = 0;
            payload[i++] = (byte) message.Headers.Count;

            foreach (var header in message.Headers)
            {
                i += WriteString(payload, i, header.Key);
                i += WriteString(payload, i, header.Value);
            }

            message.Payload.CopyTo(payload, i);

            return payload;
        }

        private int WriteString(byte[] payload, int index, string s)
        {
            var count = _encoding.GetBytes(s, 0, s.Length, payload, index + 1);
            payload[index] = (byte) count;
            return count + 1;
        }

        private int ReadString(byte[] payload, int index, out string s)
        {
            var count = payload[index];
            s = _encoding.GetString(payload, index + 1, count);
            return count + 1;
        }

        protected MessageWithHeaders Deserialize(byte[] payload)
        {
            var message = new MessageWithHeaders();

            var i = 0;
            var headerCount = payload[i++];

            for (var headerIndex = 0; headerIndex < headerCount; headerIndex++)
            {
                string key, value;

                i += ReadString(payload, i, out key);
                i += ReadString(payload, i, out value);

                message.Headers.Add(key, value);
            }

            message.Payload = new byte[payload.Length - i];
            Array.Copy(payload, i, message.Payload, 0, message.Payload.Length);

            return message;
        }

        #region Implementation of IMessageSerializer

        public byte[] Serialize(Type t, object message)
        {
            return Serialize((MessageWithHeaders) message);
        }

        public object Deserialize(Type t, byte[] payload)
        {
            return Deserialize(payload);
        }

        #endregion
    }
}