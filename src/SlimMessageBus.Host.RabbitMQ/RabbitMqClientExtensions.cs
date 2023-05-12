namespace SlimMessageBus.Host.RabbitMQ;

using global::RabbitMQ.Client;

internal static class RabbitMqClientExtensions
{
    public static void CloseAndDispose(this IModel channel)
    {
        if (channel != null)
        {
            if (!channel.IsClosed)
            {
                channel.Close();
            }
            channel.Dispose();
        }
    }

    public static void CloseAndDispose(this IConnection connection)
    {
        if (connection != null)
        {
            connection.Close();
            connection.Dispose();
        }
    }
}
