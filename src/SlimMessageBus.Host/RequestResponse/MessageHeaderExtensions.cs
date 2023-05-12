namespace SlimMessageBus.Host;

public static class MessageHeaderExtensions
{
    public static void SetHeader<T>(this IDictionary<string, object> headers, string header, T value)
    {
        if (headers is null) throw new ArgumentNullException(nameof(headers));
        if (header is null) throw new ArgumentNullException(nameof(header));

        if (value != null)
        {
            headers[header] = value;
        }
        else
        {
            headers.Remove(header);
        }
    }

    public static void SetHeader(this IDictionary<string, object> headers, string header, DateTimeOffset dt) =>
        headers.SetHeader(header, dt.ToFileTime());

    public static int GetHeaderAsInt(this IDictionary<string, object> headers, string header)
    {
        if (header is null) throw new ArgumentNullException(nameof(headers));

        var v = (int)headers[header];
        return v;
    }

    public static bool TryGetHeader(this IReadOnlyDictionary<string, object> headers, string header, out object value) =>
        headers.TryGetValue(header, out value);

    public static bool TryGetHeader(this IDictionary<string, object> headers, string header, out object value) =>
        headers.TryGetValue(header, out value);

    public static bool TryGetHeader(this IReadOnlyDictionary<string, object> headers, string header, out string value)
    {
        if (headers.TryGetValue(header, out var objValue))
        {
            value = (string)objValue;
            return true;
        }
        value = null;
        return false;
    }

    public static bool TryGetHeader(this IDictionary<string, object> headers, string header, out string value)
    {
        if (headers.TryGetValue(header, out var objValue))
        {
            value = (string)objValue;
            return true;
        }
        value = null;
        return false;
    }

    public static bool TryGetHeader(this IReadOnlyDictionary<string, object> headers, string header, out DateTimeOffset dt)
    {
        if (header != null && headers.TryGetValue(header, out var dtObj))
        {
            var dtVal = TryGetDateTimeOffset(dtObj);
            if (dtVal != null)
            {
                dt = dtVal.Value;
                return true;
            }
        }
        dt = default;
        return false;
    }

    public static bool TryGetHeader(this IDictionary<string, object> headers, string header, out DateTimeOffset dt)
    {
        if (header != null && headers.TryGetValue(header, out var dtObj))
        {
            var dtVal = TryGetDateTimeOffset(dtObj);
            if (dtVal != null)
            {
                dt = dtVal.Value;
                return true;
            }
        }
        dt = default;
        return false;
    }

    public static bool TryGetHeader(this IReadOnlyDictionary<string, object> headers, string header, out DateTimeOffset? dt)
    {
        if (headers.TryGetHeader(header, out DateTimeOffset dt2))
        {
            dt = dt2;
            return true;
        }
        dt = null;
        return false;
    }

    public static bool TryGetHeader(this IDictionary<string, object> headers, string header, out DateTimeOffset? dt)
    {
        if (headers.TryGetHeader(header, out DateTimeOffset dt2))
        {
            dt = dt2;
            return true;
        }
        dt = null;
        return false;
    }

    private static DateTimeOffset? TryGetDateTimeOffset(object value)
    {
        if (value is long dtLong || (value is string dtString && long.TryParse(dtString, out dtLong)))
        {
            return DateTimeOffset.FromFileTime(dtLong);
        }
        return null;
    }
}
