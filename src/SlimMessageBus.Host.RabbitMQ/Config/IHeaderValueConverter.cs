namespace SlimMessageBus.Host.RabbitMQ;

public interface IHeaderValueConverter
{
    object ConvertTo(object v);
    object ConvertFrom(object v);
}

