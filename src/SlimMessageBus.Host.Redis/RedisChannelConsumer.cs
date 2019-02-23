using System;
using SlimMessageBus.Host.Config;
using StackExchange.Redis;

namespace SlimMessageBus.Host.Redis
{
    public class RedisChannelConsumer : IDisposable
    {
        private readonly ChannelMessageQueue _channelMessageQueue;

        public RedisChannelConsumer(AbstractConsumerSettings consumerSettings, ISubscriber subscriber, IMessageProcessor<byte[]> messageProcessor)
        {
            _channelMessageQueue = subscriber.Subscribe(consumerSettings.Topic);
            _channelMessageQueue.OnMessage(m => messageProcessor.ProcessMessage(m.Message));
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _channelMessageQueue.Unsubscribe();
            }
        }

        #endregion
    }
}