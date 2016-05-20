using System;

namespace SlimMessageBus
{
    /// <summary>
    /// The primary access point to SlimMessageBus.
    /// </summary>
    public class MessageBus
    {
        private static Func<IMessageBus> _provider;

        public static void SetProvider(Func<IMessageBus> provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// Retrieves the <see cref="IMessageBus"/> for the current execution context.
        /// </summary>
        public static IMessageBus Current => _provider();
    }
}
