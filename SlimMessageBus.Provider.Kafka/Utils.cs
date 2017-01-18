using System;

namespace SlimMessageBus.Provider.Kafka
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
    }
}