namespace SlimMessageBus.Host
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using SlimMessageBus.Host.Serialization;

    public class MessageWithHeadersSerializer : IMessageSerializer
    {
        private readonly Encoding encoding;

        private const int StringLengthFieldSize = sizeof(short);

        public const int TypeIdNull = 0;
        public const int TypeIdString = 1;
        public const int TypeIdBool = 2;
        public const int TypeIdInt = 3;
        public const int TypeIdLong = 4;

        public MessageWithHeadersSerializer()
            : this(Encoding.ASCII)
        {
        }

        public MessageWithHeadersSerializer(Encoding encoding)
            => this.encoding = encoding;

        protected byte[] Serialize(MessageWithHeaders message)
        {
            // calculate bytes needed

            // 1 byte header count
            var payloadLength = 1;
            foreach (var header in message.Headers)
            {
                // 2 byte for key length + string length
                payloadLength += StringLengthFieldSize + encoding.GetByteCount(header.Key);
                // TypeId discriminator + value length in bytes
                payloadLength += CalculateWriteObjectByteLength(header.Value);
            }
            payloadLength += message.Payload?.Length ?? 0;

            // allocate bytes
            var payload = new byte[payloadLength];

            // write bytes
            var i = 0;
            payload[i++] = (byte)message.Headers.Count;

            foreach (var header in message.Headers)
            {
                i += WriteString(payload, i, header.Key);
                i += WriteObject(payload, i, header.Value);
            }

            message.Payload?.CopyTo(payload, i);

            return payload;
        }

        private int WriteObject(byte[] payload, int index, object v)
        {
            switch (v)
            {
                case null:
                    payload[index] = TypeIdNull;
                    return 1;
                case string s:
                    payload[index] = TypeIdString;
                    return 1 + WriteString(payload, index + 1, s);
                case bool b:
                    payload[index] = TypeIdBool;
                    return 1 + WriteBool(payload, index + 1, b);
                case int i:
                    payload[index] = TypeIdInt;
                    return 1 + WriteInt(payload, index + 1, i);
                case long l:
                    payload[index] = TypeIdLong;
                    return 1 + WriteLong(payload, index + 1, l);
                default:
                    throw new InvalidOperationException($"Not supported header value type {v?.GetType().FullName ?? "(null)"}");
            }
        }

        private int CalculateWriteObjectByteLength(object v)
        {
            var byteLength = v switch
            {
                null => 0,
                string s => StringLengthFieldSize + encoding.GetByteCount(s),
                bool _ => sizeof(byte),
                int _ => sizeof(int),
                long _ => sizeof(long),
                _ => throw new InvalidOperationException($"Not supported header value type {v?.GetType().FullName ?? "(null)"}"),
            };
            return 1 + byteLength;
        }

        private int WriteString(byte[] payload, int index, string s)
        {
            var count = encoding.GetBytes(s, 0, s.Length, payload, index + StringLengthFieldSize);

            // Write string length (byte length)
            BitConverter.TryWriteBytes(payload.AsSpan(index), (short)count);

            return count + StringLengthFieldSize;
        }

        private static int WriteBool(byte[] payload, int index, bool v)
        {
            payload[index] = v ? (byte)1 : (byte)0;
            return sizeof(byte);
        }

        private static int WriteInt(byte[] payload, int index, int v)
        {
            BitConverter.TryWriteBytes(payload.AsSpan(index), v);
            return sizeof(int);
        }

        private static int WriteLong(byte[] payload, int index, long v)
        {
            BitConverter.TryWriteBytes(payload.AsSpan(index), v);
            return sizeof(long);
        }

        private int ReadObject(byte[] payload, int index, out object o)
        {
            int byteLength;
            var typeId = payload[index];
            switch (typeId)
            {
                case TypeIdNull:
                    byteLength = 0;
                    o = null;
                    break;
                case TypeIdString:
                    byteLength = ReadString(payload, index + 1, out var s);
                    o = s;
                    break;
                case TypeIdBool:
                    byteLength = ReadBool(payload, index + 1, out var b);
                    o = b;
                    break;
                case TypeIdInt:
                    byteLength = ReadInt(payload, index + 1, out var i);
                    o = i;
                    break;
                case TypeIdLong:
                    byteLength = ReadLong(payload, index + 1, out var l);
                    o = l;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown field type with discriminator {typeId}");
            }

            // Type Discriminator length (1 byte) + value length
            return 1 + byteLength;
        }

        private int ReadString(byte[] payload, int index, out string v)
        {
            var count = BitConverter.ToInt16(payload, index);
            v = encoding.GetString(payload, index + StringLengthFieldSize, count);
            return count + StringLengthFieldSize;
        }

        private static int ReadBool(byte[] payload, int index, out bool v)
        {
            v = payload[index] == 1;
            return sizeof(byte);
        }

        private static int ReadInt(byte[] payload, int index, out int v)
        {
            v = BitConverter.ToInt32(payload, index);
            return sizeof(int);
        }

        private static int ReadLong(byte[] payload, int index, out long v)
        {
            v = BitConverter.ToInt64(payload, index);
            return sizeof(long);
        }

        protected MessageWithHeaders Deserialize(byte[] payload)
        {
            if (payload is null) throw new ArgumentNullException(nameof(payload));

            var messageHeaders = new Dictionary<string, object>();

            var i = 0;
            var headerCount = payload[i++];

            for (var headerIndex = 0; headerIndex < headerCount; headerIndex++)
            {
                i += ReadString(payload, i, out var key);
                i += ReadObject(payload, i, out var value);

                messageHeaders.Add(key, value);
            }

            byte[] messagePayload = null;

            var payloadSize = payload.Length - i;
            if (payloadSize > 0)
            {
                messagePayload = new byte[payload.Length - i];
                Array.Copy(payload, i, messagePayload, 0, messagePayload.Length);
            }

            return new MessageWithHeaders(messagePayload, messageHeaders);
        }

        #region Implementation of IMessageSerializer

        public byte[] Serialize(Type t, object message)
            => Serialize((MessageWithHeaders)message);

        public object Deserialize(Type t, byte[] payload)
            => Deserialize(payload);

        #endregion
    }
}