using System.Collections.Generic;

namespace SlimMessageBus.Host
{
    public class MessageWithHeaders
    {
        public IDictionary<string, string> Headers { get; set; }
        public byte[] Payload { get; set; }

        public MessageWithHeaders()
        {
            Headers = new Dictionary<string, string>();
        }

        public MessageWithHeaders(byte[] payload)
        {
            Headers = new Dictionary<string, string>();
            Payload = payload;
        }
    }
}