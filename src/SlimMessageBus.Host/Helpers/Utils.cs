namespace SlimMessageBus.Host
{
    using System;
    using Microsoft.Extensions.Logging;

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


        public static void DisposeSilently(this IDisposable disposable, string name, ILogger logger)
        {
            disposable.DisposeSilently(e => logger.LogWarning(e, "Error occured while disposing {0}", name));
        }

        public static void DisposeSilently(this IDisposable disposable, Func<string> nameFunc, ILogger logger)
        {
            disposable.DisposeSilently(e => logger.LogWarning(e, "Error occured while disposing {0}", nameFunc()));
        }
    }
}