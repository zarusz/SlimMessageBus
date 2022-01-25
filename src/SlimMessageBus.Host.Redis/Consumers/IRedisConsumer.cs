namespace SlimMessageBus.Host.Redis
{
    using System;

    public interface IRedisConsumer : IAsyncDisposable, IConsumerControl
    {
    }
}