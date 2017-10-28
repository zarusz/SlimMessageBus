using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.InMemory
{
    public class InMemoryMessageBus : MessageBusBase
    {
        private static readonly ILog Log = LogManager.GetLogger<InMemoryMessageBus>();


        public InMemoryMessageBus(MessageBusSettings settings) 
            : base(settings)
        {
        }

        #region Overrides of MessageBusBase

        public override Task Publish(Type messageType, byte[] payload, string topic)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
