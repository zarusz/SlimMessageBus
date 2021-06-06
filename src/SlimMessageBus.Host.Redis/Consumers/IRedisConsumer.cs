namespace SlimMessageBus.Host.Redis
{
    using System;
    using System.Threading.Tasks;

    public interface IRedisConsumer : IDisposable
    {
        Task Start();
        Task Finish();
    }
}