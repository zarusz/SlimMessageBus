using System;

namespace SlimMessageBus
{
    public interface IMessageBus : IDisposable, IRequestResponseBus, IPublishBus
    {
    }
}