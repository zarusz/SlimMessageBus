using System;

namespace SlimMessageBus
{
    public interface IMessageBus : IMessageBusPublisher, IMessageBusSubscriber, IDisposable
    {
    }                      
}