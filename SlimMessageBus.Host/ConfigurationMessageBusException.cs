using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlimMessageBus.Host
{
    public class ConfigurationMessageBusException : MessageBusException
    {
        public ConfigurationMessageBusException(string message) : base(message)
        {
        }

        public ConfigurationMessageBusException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
