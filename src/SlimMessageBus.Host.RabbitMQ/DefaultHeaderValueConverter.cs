namespace SlimMessageBus.Host.RabbitMQ;

using System.Text;

/// <summary>
/// Peforms mappingg of the header value from the native RabbitMq client to SMB and vice vera.
/// Converts the string into a byte[] UTF-8 when sending the message to RabbitMq client, and converts that back to avoid the known problem: https://github.com/rabbitmq/rabbitmq-dotnet-client/issues/415
/// </summary>
public class DefaultHeaderValueConverter : IHeaderValueConverter
{
    private readonly Encoding _encoding;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="encoding"></param>
    public DefaultHeaderValueConverter(Encoding encoding = null)
    {
        _encoding = encoding ?? Encoding.UTF8;
    }

    /// <inheritdoc/>
    public object ConvertTo(object v) => v switch
    {
        string str => _encoding.GetBytes(str),
        _ => v
    };

    /// <inheritdoc/>
    public object ConvertFrom(object v) => v switch
    {
        byte[] bytes => _encoding.GetString(bytes),
        _ => v
    };
}