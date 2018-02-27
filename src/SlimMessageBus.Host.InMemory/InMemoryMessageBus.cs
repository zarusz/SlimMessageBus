using System;
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

        public override Task PublishToTransport(Type messageType, object message, string topic, byte[] payload)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
