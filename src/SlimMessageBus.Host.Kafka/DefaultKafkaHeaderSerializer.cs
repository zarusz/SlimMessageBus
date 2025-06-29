﻿namespace SlimMessageBus.Host.Kafka;

using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Serializes the headers values by doing a Convert.ToString(value, CultureInfo.InvariantCulture) on them.
/// </summary>
public class DefaultKafkaHeaderSerializer : IMessageSerializer, IMessageSerializerProvider
{
    private readonly Encoding _encoding;
    private readonly bool _inferType;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="encoding"></param>
    /// <param name="inferType">Should the string attempted to be parsed against the common primitive types.</param>
    public DefaultKafkaHeaderSerializer(Encoding encoding = null, bool inferType = false)
    {
        _encoding = encoding ?? Encoding.UTF8;
        _inferType = inferType;
    }

    #region Implementation of IMessageSerializer

    public byte[] Serialize(Type messageType, IDictionary<string, object> headers, object message, object transportMessage)
    {
        if (message == null) return null;
        var payload = _encoding.GetBytes(Convert.ToString(message, CultureInfo.InvariantCulture));
        return payload;
    }

    public object Deserialize(Type messageType, IReadOnlyDictionary<string, object> headers, byte[] payload, object transportMessage)
    {
        if (payload == null) return null;

        var str = _encoding.GetString(payload);
        if (_inferType)
        {
            if (long.TryParse(str, out var valInt))
            {
                return valInt;
            }
            if (bool.TryParse(str, out var valBool))
            {
                return valBool;
            }
            if (Guid.TryParse(str, out var valGuid))
            {
                return valGuid;
            }
            if (decimal.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var valDecimal))
            {
                return valDecimal;
            }
        }
        return str;
    }

    #endregion

    public IMessageSerializer GetSerializer(string path) => this;
}
