using System;
using Microsoft.Extensions.Logging;

namespace SlimMessageBus.Host
{
    public static class Utils
    {
        public static void DisposeSilently(this IDisposable disposable, Action<Exception> failed)
        {
            try
            {
                disposable?.Dispose();
            }
            catch (Exception e)
            {
                if (failed != null)
                {
                    failed(e);

                }
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