namespace SlimMessageBus.Host.Config;

public interface IConsumerEvents
{
    /// <summary>
    /// Called whenever a consumer receives a message (and prior message is handled)
    /// Arguments are <see cref="IMessageBus"/>, consumer settings, typed message, topic/queue name, native transport message.
    /// </summary>
    Action<IMessageBus, AbstractConsumerSettings, object, string, object> OnMessageArrived { get; set; }

    /// <summary>
    /// Called whenever a consumer finishes handling a message (after message is handled)
    /// Arguments are <see cref="IMessageBus"/>, consumer settings, typed message, topic/queue name, native transport message.
    /// </summary>
    Action<IMessageBus, AbstractConsumerSettings, object, string, object> OnMessageFinished { get; set; }

    /// <summary>
    /// Called whenever a consumer receives an expired message.
    /// Arguments are <see cref="IMessageBus"/>, consumer settings, typed message, native transport message.
    /// </summary>
    Action<IMessageBus, AbstractConsumerSettings, object, object> OnMessageExpired { get; set; }

    /// <summary>
    /// Called whenever a consumer errors out while processing the message.
    /// Arguments are <see cref="IMessageBus"/>, consumer settings, typed message, exception, native transport message.
    /// </summary>
    Action<IMessageBus, AbstractConsumerSettings, object, Exception, object> OnMessageFault { get; set; }
}