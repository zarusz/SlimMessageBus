using System;

namespace SlimMessageBus.Host
{
    public class Assert
    {
        public static void IsTrue(bool value, Func<Exception> exceptionFactory)
        {
            if (!value)
            {
                var e = exceptionFactory();
                throw e;
            }
        }

        public static void IsFalse(bool value, Func<Exception> exceptionFactory)
        {
            IsTrue(!value, exceptionFactory);
        }
    }
}