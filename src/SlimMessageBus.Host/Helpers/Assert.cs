namespace SlimMessageBus.Host;

public static class Assert
{
    public static void IsTrue(bool value, Func<Exception> exceptionFactory)
    {
        if (exceptionFactory == null)
        {
            throw new ArgumentNullException(nameof(exceptionFactory));
        }

        if (!value)
        {
            var e = exceptionFactory();
            throw e;
        }
    }

    public static void IsNotNull(object value, Func<Exception> exceptionFactory)
    {
        IsTrue(value != null, exceptionFactory);
    }
}