using System;

namespace SlimMessageBus
{
    /// <summary>
    /// Lookup helper of the <see cref="IMessageBus"/> for the current execution context.
    /// </summary>
    public static class MessageBus
    {
        private static Func<IMessageBus> _provider;

        public static bool IsProviderSet() => _provider == null;

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
