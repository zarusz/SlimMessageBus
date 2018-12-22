using System;
using System.Text;
using SlimMessageBus.Host.Serialization;

namespace SlimMessageBus.Host
{
    public class MessageWithHeadersSerializer : IMessageSerializer
    {
        private readonly Encoding _encoding;

        private const int StringLengthFieldSize = 2;

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
                // 2 byte for key length
                n += StringLengthFieldSize + _encoding.GetByteCount(header.Key);
                // 2 byte for value length
                n += StringLengthFieldSize + _encoding.GetByteCount(header.Value);
            }
            n += message.Payload?.Length ?? 0;

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

            message.Payload?.CopyTo(payload, i);

            return payload;
        }

        private int WriteString(byte[] payload, int index, string s)
        {
            var count = _encoding.GetBytes(s, 0, s.Length, payload, index + StringLengthFieldSize);
            payload[index] = (byte)(count & 255);
            payload[index+1] = (byte)((count >> 8) & 255);
            return count + StringLengthFieldSize;
        }

        private int ReadString(byte[] payload, int index, out string s)
        {
            var count = (ushort) (payload[index] | (payload[index+1] << 8));
            s = _encoding.GetString(payload, index + StringLengthFieldSize, count);
            return count + StringLengthFieldSize;
        }

        protected MessageWithHeaders Deserialize(byte[] payload)
        {
            var message = new MessageWithHeaders();

            var i = 0;
            var headerCount = payload[i++];

            for (var headerIndex = 0; headerIndex < headerCount; headerIndex++)
            {
                i += ReadString(payload, i, out var key);
                i += ReadString(payload, i, out var value);

                message.Headers.Add(key, value);
            }

            var payloadSize = payload.Length - i;
            if (payloadSize > 0)
            {
                message.Payload = new byte[payload.Length - i];
                Array.Copy(payload, i, message.Payload, 0, message.Payload.Length);
            }
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