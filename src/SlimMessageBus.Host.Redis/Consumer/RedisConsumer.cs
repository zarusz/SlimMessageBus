using Common.Logging;
using SlimMessageBus.Host.Config;
using System;

namespace SlimMessageBus.Host.Redis.Consumer
{
    internal class RedisConsumer : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<RedisConsumer>();

        private readonly ConsumerInstancePool<RedisMessage> _instancePool;
        private readonly MessageQueueWorker<RedisMessage> _queueWorker;

        public RedisConsumer(RedisMessageBus messageBus, ConsumerSettings consumerSettings)
        {
            _instancePool = new ConsumerInstancePool<RedisMessage>(consumerSettings, messageBus, e => e.Value);
            _queueWorker = new MessageQueueWorker<RedisMessage>(_instancePool, new CheckpointTrigger(consumerSettings));
        }

        public void Dispose()
        {
            _instancePool.DisposeSilently(nameof(ConsumerInstancePool<RedisMessage>), Log);
        }

        public bool OnSubmit(RedisMessage message)
        {
            return _queueWorker.Submit(message);
        }
    }
}
