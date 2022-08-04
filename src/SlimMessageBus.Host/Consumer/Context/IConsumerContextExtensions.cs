namespace SlimMessageBus.Host;

public static class IConsumerContextExtensions
{
    public static T GetPropertyOrDefault<T>(this IConsumerContext context, string key)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        if (context.Properties.TryGetValue(key, out var value))
        {
            return (T)value;
        }
        return default;
    }
}
