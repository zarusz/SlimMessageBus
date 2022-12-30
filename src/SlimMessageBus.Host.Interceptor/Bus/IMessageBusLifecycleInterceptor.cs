namespace SlimMessageBus.Host.Interceptor;

public interface IMessageBusLifecycleInterceptor : IInterceptor
{
    Task OnBusLifecycle(MessageBusLifecycleEventType eventType, IMessageBus bus);
}
