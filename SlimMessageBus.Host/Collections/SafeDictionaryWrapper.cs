using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SlimMessageBus.Host.Collections
{
    /// <summary>
    /// Dictionary wrapper that exposes a <see cref="ReadOnlyDictionary{TKey,TValue}"/> for read, while for mutation exposes thread-safe methods.
    /// Internally a dictionary is maintained and any mutation is locked. Each mutation causes a new <see cref="ReadOnlyDictionary{TKey,TValue}"/> to be created.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class SafeDictionaryWrapper<TKey, TValue>
    {
        private IDictionary<TKey, TValue> _dict;
        public IDictionary<TKey, TValue> Dictonary { get; protected set; }
        public Func<TKey, TValue> ValueFactory { get; set; }

        public SafeDictionaryWrapper()
        {
            _dict = new Dictionary<TKey, TValue>();
            OnChanged();
        }

        public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
        {
            TValue value;
            // check if we have the EventHubClient already for the HubName
            // ReSharper disable once InconsistentlySynchronizedField
            if (!_dict.TryGetValue(key, out value))
            {
                lock (this)
                {
                    // double check if another thread did create it in meantime (before lock)
                    if (!_dict.TryGetValue(key, out value))
                    {
                        value = factory(key);
                        // allocate a new dictonary to avoid mutation while reading in another thread
                        _dict = new Dictionary<TKey, TValue>(_dict)
                        {
                            {key, value}
                        };
                        OnChanged();
                    }
                }
            }
            return value;
        }

        private void OnChanged()
        {
            Dictonary = new ReadOnlyDictionary<TKey, TValue>(_dict);
        }

        public TValue GetOrAdd(TKey key)
        {
            return GetOrAdd(key, ValueFactory);
        }

        public void Clear()
        {
            lock (this)
            {
                _dict.Clear();
                OnChanged();
            }
        }
    }

}
