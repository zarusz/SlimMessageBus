namespace SlimMessageBus.Host
{
    using System;

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

        public static void IsFalse(bool value, Func<Exception> exceptionFactory)
        {
            IsTrue(!value, exceptionFactory);
        }

        public static void IsNotNull(object value, Func<Exception> exceptionFactory)
        {
            IsTrue(value != null, exceptionFactory);
        }
    }
}