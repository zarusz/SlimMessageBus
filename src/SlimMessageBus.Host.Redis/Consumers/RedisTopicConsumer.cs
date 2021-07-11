namespace SlimMessageBus.Host.Redis
{
    using System;
    using System.Threading.Tasks;
    using SlimMessageBus.Host.Config;
    using StackExchange.Redis;

    public class RedisTopicConsumer : IRedisConsumer
    {
        private readonly string _topic;
        private readonly ISubscriber _subscriber;
        private readonly IMessageProcessor<byte[]> _messageProcessor;

        private ChannelMessageQueue _channelMessageQueue;

        public RedisTopicConsumer(AbstractConsumerSettings consumerSettings, ISubscriber subscriber, IMessageProcessor<byte[]> messageProcessor)
        {
            _ = consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings));

            _topic = consumerSettings.Path;
            _subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
            _messageProcessor = messageProcessor;
        }

        public Task Start()
        {
            _channelMessageQueue = _subscriber.Subscribe(_topic);
            _channelMessageQueue.OnMessage(m => _messageProcessor.ProcessMessage(m.Message));

            return Task.CompletedTask;
        }

        public Task Finish()
        {
            UnsubscribeInternal();

            return Task.CompletedTask;
        }

        private void UnsubscribeInternal()
        {
            if (_channelMessageQueue != null)
            {
                _channelMessageQueue.Unsubscribe();
                _channelMessageQueue = null;
            }
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
                UnsubscribeInternal();

                _messageProcessor.Dispose();
            }
        }

        #endregion
    }
}