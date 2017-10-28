using System.Collections.Generic;

namespace SlimMessageBus.Host.Config
{
    public abstract class HasProviderExtensions
    {
        /// <summary>
        /// Provider specific properties bag.
        /// </summary>
        public IDictionary<string, object> Properties { get; protected set; }

        protected HasProviderExtensions()
        {
            Properties = new Dictionary<string, object>();
        }

        public T GetOrDefault<T>(string key, T defaultValue)
        {
            object value;
            if (Properties.TryGetValue(key, out value))
            {
                return (T) value;
            }
            return defaultValue;
        }
    }
}