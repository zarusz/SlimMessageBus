namespace SlimMessageBus.Host
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    public class ConsumerContext : IConsumerContext
    {
        private readonly IReadOnlyDictionary<string, object> propertiesWrapper;
        private readonly IDictionary<string, object> properties;

        public ConsumerContext()
        {
            properties = new Dictionary<string, object>();
            propertiesWrapper = new ReadOnlyDictionary<string, object>(properties);
        }

        public IReadOnlyDictionary<string, object> Headers { get; set; }

        public IReadOnlyDictionary<string, object> Properties => propertiesWrapper;

        public void SetProperty(string key, object value) => properties[key] = value;
    }
}
