using Common.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace SlimMessageBus.Host.Serialization.Hybrid
{
    /// <summary>
    /// <see cref="IMessageSerializer"/> implementation that delegates (routes) the serialization to the respective serializer based on message type.
    /// </summary>
    public class HybridMessageSerializer : IMessageSerializer
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IList<IMessageSerializer> _serializers = new List<IMessageSerializer>();
        private readonly IDictionary<Type, IMessageSerializer> _serializerByType = new Dictionary<Type, IMessageSerializer>();
        public IMessageSerializer DefaultSerializer { get; set; }

        public HybridMessageSerializer()
        {
        }

        public HybridMessageSerializer(IDictionary<IMessageSerializer, Type[]> registration)
        {
            foreach (var entry in registration)
            {
                Add(entry.Key, entry.Value);
            }
        }

        public void Add(IMessageSerializer serializer, params Type[] supportedTypes)
        {
            if (_serializers.Count == 0 && DefaultSerializer == null)
            {
                DefaultSerializer = serializer;
            }

            _serializers.Add(serializer);
            foreach (var type in supportedTypes)
            {
                _serializerByType.Add(type, serializer);
            }
        }

        protected virtual IMessageSerializer MatchSerializer(Type t)
        {
            if (_serializers.Count == 0)
            {
                throw new InvalidOperationException("No serializers registered.");
            }

            if (!_serializerByType.TryGetValue(t, out var serializer))
            {
                // use first as default
                Log.TraceFormat(CultureInfo.InvariantCulture, "Serializer for type {0} not registered, will use default serializer", t);
                serializer = DefaultSerializer;
            }

            Log.DebugFormat(CultureInfo.InvariantCulture, "Serializer for type {0} will be {1}", t, serializer);
            return serializer;
        }

        public object Deserialize(Type t, byte[] payload)
        {
            var serializer = MatchSerializer(t);
            return serializer.Deserialize(t, payload);
        }

        public byte[] Serialize(Type t, object message)
        {
            var serializer = MatchSerializer(t);
            return serializer.Serialize(t, message);
        }
    }
}
