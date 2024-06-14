namespace SlimMessageBus.Host.Interceptor;

public enum MessageBusLifecycleEventType
{
    /// <summary>
    /// Invoked when the master bus is created.
    /// Can be used to initalize any resource before the messages are produced or consumed.
    /// </summary>
    Created,
    Starting,
    Started,
    Stopping,
    Stopped
}