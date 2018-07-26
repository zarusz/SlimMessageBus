using Common.Logging;
using System;
using System.Globalization;

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
                failed(e);
            }
        }

        public static void DisposeSilently(this IDisposable disposable, string name, ILog log)
        {
            disposable.DisposeSilently(e => log.WarnFormat(CultureInfo.InvariantCulture, "Error occured while disposing {0}. {1}", name, e));
        }

        public static void DisposeSilently(this IDisposable disposable, Func<string> nameFunc, ILog log)
        {
            disposable.DisposeSilently(e => log.WarnFormat(CultureInfo.InvariantCulture, "Error occured while disposing {0}. {1}", nameFunc(), e));
        }

        public static void InvokeSilently(Action action, ILog log, string format, params object[] args)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                log.WarnFormat(CultureInfo.InvariantCulture, format, e, args);
            }
        }
    }
}