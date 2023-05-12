namespace SlimMessageBus.Host.RabbitMQ;

using System.Text;

/// <summary>
/// Serializes the headers values by doing a Convert.ToString(value, CultureInfo.InvariantCulture) on them.
/// </summary>
public class DefaultRabbitMqHeaderSerializer : IMessageSerializer
{
    private readonly Encoding _encoding;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="encoding"></param>
    /// <param name="inferType">Should the string attempted to be parsed against the common primitive types.</param>
    public DefaultRabbitMqHeaderSerializer(Encoding encoding = null)
    {
        _encoding = encoding ?? Encoding.UTF8;
    }

    #region Implementation of IMessageSerializer

    public byte[] Serialize(Type t, object message)
    {
        if (message == null) return null;

        var payload = message switch
        {
            string str => _encoding.GetBytes(str),
            _ => null
        };
        return payload;
    }

    public object Deserialize(Type t, byte[] payload)
    {
        if (payload == null) return null;

        try
        {
            var str = _encoding.GetString(payload);
            return str;
        }
        catch
        {
            // it's not a string
        }

        return null;
    }

    #endregion
}