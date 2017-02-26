using System;
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

        public void SetHeader(string header, string value)
        {
            Headers[header] = value;
        }

        public void SetHeader(string header, DateTimeOffset dt)
        {
            Headers[header] = dt.ToFileTime().ToString();
        }

        public bool TryGetHeader(string header, out string value)
        {
            return Headers.TryGetValue(header, out value);
        }


        public bool TryGetHeader(string header, out DateTimeOffset dt)
        {
            string dtStr;
            if (Headers.TryGetValue(header, out dtStr))
            {
                var dtLong = long.Parse(dtStr);
                dt = DateTimeOffset.FromFileTime(dtLong);
                return true;
            }
            dt = default(DateTimeOffset);
            return false;
        }

        public bool TryGetHeader(string header, out DateTimeOffset? dt)
        {
            DateTimeOffset dt2;

            if (TryGetHeader(header, out dt2))
            {
                dt = dt2;
                return true;
            }
            dt = null;
            return false;
        }
    }
}