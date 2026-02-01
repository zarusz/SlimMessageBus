namespace SlimMessageBus.Host.RabbitMQ;

using global::RabbitMQ.Client;

/// <summary>
/// Provides access to the RabbitMQ channel.
/// Note: IChannel is thread-safe in RabbitMQ.Client v7+ and can be used concurrently without external locking.
/// </summary>
public interface IRabbitMqChannel
{
    IChannel Channel { get; }
}
