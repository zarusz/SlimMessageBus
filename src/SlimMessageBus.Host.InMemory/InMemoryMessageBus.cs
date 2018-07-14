using System;
using System.Threading.Tasks;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.InMemory
{
    public class InMemoryMessageBus : MessageBusBase
    {
        public InMemoryMessageBus(MessageBusSettings settings) 
            : base(settings)
        {
        }

        #region Overrides of MessageBusBase

        public override Task PublishToTransport(Type messageType, object message, string topic, byte[] payload)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
