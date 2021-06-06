namespace SlimMessageBus.Host.Redis
{
    using System;
    using System.Threading.Tasks;
    using SlimMessageBus.Host.Config;
    using StackExchange.Redis;

    public class RedisTopicConsumer : IRedisConsumer
    {
        private readonly ChannelMessageQueue _channelMessageQueue;

        public RedisTopicConsumer(AbstractConsumerSettings consumerSettings, ISubscriber subscriber, IMessageProcessor<byte[]> messageProcessor)
        {
            if (consumerSettings is null) throw new ArgumentNullException(nameof(consumerSettings));
            if (subscriber is null) throw new ArgumentNullException(nameof(subscriber));

            _channelMessageQueue = subscriber.Subscribe(consumerSettings.Path);
            _channelMessageQueue.OnMessage(m => messageProcessor.ProcessMessage(m.Message));
        }

        public Task Start() => Task.CompletedTask;
        public Task Finish() => Task.CompletedTask;

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