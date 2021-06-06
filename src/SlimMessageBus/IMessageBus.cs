namespace SlimMessageBus
{
    using System;

    public interface IMessageBus : IDisposable, IRequestResponseBus, IPublishBus
    {
    }
}