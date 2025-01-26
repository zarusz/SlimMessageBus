namespace SlimMessageBus.Host;

public static class Utils
{
    public static void DisposeSilently(this IDisposable disposable, Action<Exception> failed = null)
    {
        try
        {
            disposable?.Dispose();
        }
        catch (Exception e)
        {
            failed?.Invoke(e);
        }
    }

    public static async ValueTask DisposeSilently(this IAsyncDisposable disposable, Action<Exception> failed = null)
    {
        try
        {
            if (disposable != null)
            {
                await disposable.DisposeAsync();
            }
        }
        catch (Exception e)
        {
            failed?.Invoke(e);
        }
    }

    public static void DisposeSilently(this IDisposable disposable, string name, ILogger logger)
        => disposable.DisposeSilently(e => logger.LogWarning(e, "Error occurred while disposing {Name}", name));

    public static ValueTask DisposeSilently(this IAsyncDisposable disposable, Func<string> nameFunc, ILogger logger)
        => disposable.DisposeSilently(e => logger.LogWarning(e, "Error occurred while disposing {Name}", nameFunc()));

    public static ValueTask DisposeSilently(this IAsyncDisposable disposable, string name, ILogger logger)
        => disposable.DisposeSilently(e => logger.LogWarning(e, "Error occurred while disposing {Name}", name));

    public static string JoinOrSingle<T>(this T[] values, Func<T, string> selector, string separator = ",") => values.Length switch
    {
        0 => string.Empty,
        1 => selector(values[0]),
        _ => string.Join(separator, values.Select(selector))
    };
}