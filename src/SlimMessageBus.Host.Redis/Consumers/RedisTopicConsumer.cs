namespace SlimMessageBus.Host.Redis
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;
    using StackExchange.Redis;

    public class RedisTopicConsumer : IRedisConsumer
    {
        private readonly ILogger<RedisTopicConsumer> logger;
        private readonly string topic;
        private readonly ISubscriber subscriber;
        private IMessageProcessor<byte[]> messageProcessor;
        private ChannelMessageQueue channelMessageQueue;

        public RedisTopicConsumer(ILogger<RedisTopicConsumer> logger, AbstractConsumerSettings consumerSettings, ISubscriber subscriber, IMessageProcessor<byte[]> messageProcessor)
        {
            _ = consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings));

            this.logger = logger;
            topic = consumerSettings.Path;
            this.subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
            this.messageProcessor = messageProcessor;
        }

        public async Task Start()
        {
            logger.LogInformation("Subscribing to redis channel {Topic}", topic);
            channelMessageQueue = await subscriber.SubscribeAsync(topic);
            channelMessageQueue.OnMessage(m => messageProcessor.ProcessMessage(m.Message));
        }

        public Task Stop()
        {
            return UnsubscribeInternal();
        }

        private async Task UnsubscribeInternal()
        {
            if (channelMessageQueue != null)
            {
                logger.LogInformation("Unsubscribing from redis channel {Topic}", topic);
                await channelMessageQueue.UnsubscribeAsync();
                channelMessageQueue = null;
            }
        }

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            await UnsubscribeInternal();

            if (messageProcessor != null)
            {
                await messageProcessor.DisposeAsync();
                messageProcessor = null;
            }
        }

        #endregion
    }
}