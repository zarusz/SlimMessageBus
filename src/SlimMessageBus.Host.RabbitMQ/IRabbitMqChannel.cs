namespace SlimMessageBus.Host.RabbitMQ;

using global::RabbitMQ.Client;

public interface IRabbitMqChannel
{
    public IModel Channel { get; }
    public object ChannelLock { get; }
}
