namespace SlimMessageBus.Host.GooglePubSub;

using System.Globalization;

/// <summary>
/// Default serializer for Pub/Sub attributes. Type markers preserve common SlimMessageBus header value types.
/// </summary>
public class DefaultGooglePubSubHeaderSerializer : IGooglePubSubHeaderSerializer
{
    private const string Prefix = "smb:";

    public string Serialize(string key, object value) => value switch
    {
        null => $"{Prefix}null:",
        bool boolean => $"{Prefix}bool:{boolean}",
        byte number => $"{Prefix}byte:{number.ToString(CultureInfo.InvariantCulture)}",
        short number => $"{Prefix}short:{number.ToString(CultureInfo.InvariantCulture)}",
        int number => $"{Prefix}int:{number.ToString(CultureInfo.InvariantCulture)}",
        long number => $"{Prefix}long:{number.ToString(CultureInfo.InvariantCulture)}",
        float number => $"{Prefix}float:{number.ToString("R", CultureInfo.InvariantCulture)}",
        double number => $"{Prefix}double:{number.ToString("R", CultureInfo.InvariantCulture)}",
        decimal number => $"{Prefix}decimal:{number.ToString(CultureInfo.InvariantCulture)}",
        Guid guid => $"{Prefix}guid:{guid:D}",
        DateTime dateTime => $"{Prefix}datetime:{dateTime.ToString("O", CultureInfo.InvariantCulture)}",
        DateTimeOffset dateTimeOffset => $"{Prefix}datetimeoffset:{dateTimeOffset.ToString("O", CultureInfo.InvariantCulture)}",
        _ => value.ToString()
    };

    public object Deserialize(string key, string value)
    {
        if (value == null || !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return value;
        }

        var separator = value.IndexOf(':', Prefix.Length);
        if (separator < 0)
        {
            return value;
        }

        var type = value.Substring(Prefix.Length, separator - Prefix.Length);
        var serialized = value.Substring(separator + 1);
        return type switch
        {
            "null" => null,
            "bool" when bool.TryParse(serialized, out var parsed) => parsed,
            "byte" when byte.TryParse(serialized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            "short" when short.TryParse(serialized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            "int" when int.TryParse(serialized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            "long" when long.TryParse(serialized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            "float" when float.TryParse(serialized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            "double" when double.TryParse(serialized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            "decimal" when decimal.TryParse(serialized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
            "guid" when Guid.TryParse(serialized, out var parsed) => parsed,
            "datetime" when DateTime.TryParse(serialized, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) => parsed,
            "datetimeoffset" when DateTimeOffset.TryParse(serialized, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) => parsed,
            _ => value
        };
    }
}
