namespace SlimMessageBus.Host.Collections
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    /// <summary>
    /// Dictionary wrapper that exposes a <see cref="ReadOnlyDictionary{TKey,TValue}"/> snapshot for read, while for mutation exposes thread-safe methods.
    /// Internally a dictionary is maintained and any change is synchronized. When read access is performed after a change happened a new <see cref="ReadOnlyDictionary{TKey,TValue}"/> snapshot is created.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class SafeDictionaryWrapper<TKey, TValue>
    {
        private IDictionary<TKey, TValue> _mutableDict;
        private IDictionary<TKey, TValue> _readonlyDict;

        /// <summary>
        /// Provides read only snapshot of the mutable internal dictionary
        /// </summary>
        public IDictionary<TKey, TValue> Dictonary
        {
            get
            {
                if (_readonlyDict == null)
                {
                    lock (this)
                    {
                        if (_readonlyDict == null)
                        {
                            // Lazily create the read only snapshot
                            _readonlyDict = new ReadOnlyDictionary<TKey, TValue>(_mutableDict);
                        }
                    }
                }
                return _readonlyDict;
            }
        }

        private readonly Func<TKey, TValue> _valueFactory;

        public SafeDictionaryWrapper()
            : this(null)
        {
        }

        public SafeDictionaryWrapper(Func<TKey, TValue> valueFactory)
        {
            _mutableDict = new Dictionary<TKey, TValue>();
            _valueFactory = valueFactory;
            OnChanged();
        }

        public bool TryGet(TKey key, out TValue value)
            => _mutableDict.TryGetValue(key, out value);

        public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
        {
            if (factory is null) throw new ArgumentNullException(nameof(factory));

            // check if we have the value already for the key
            if (!_mutableDict.TryGetValue(key, out var value))
            {
                lock (this)
                {
                    // double check if another thread did create it in meantime (before lock)
                    if (!_mutableDict.TryGetValue(key, out value))
                    {
                        value = factory(key);
                        Set(key, value);
                    }
                }
            }
            return value;
        }

        public void Set(TKey key, TValue value)
        {
            lock (this)
            {
                // allocate a new dictonary to avoid mutation while reading in another thread
                _mutableDict = new Dictionary<TKey, TValue>(_mutableDict)
                {
                    [key] = value
                };
                OnChanged();
            }
        }

        public TValue GetOrAdd(TKey key)
        {
            if (_valueFactory == null)
            {
                throw new InvalidOperationException("No value factory provided");
            }
            return GetOrAdd(key, _valueFactory);
        }

        public void Clear(Action<TValue> action = null)
        {
            lock (this)
            {
                if (action != null)
                {
                    ForEach(action);
                }
                _mutableDict.Clear();
                OnChanged();
            }
        }

        public IReadOnlyCollection<TValue> ClearAndSnapshot()
        {
            lock (this)
            {
                var snapshot = Dictonary.Values.ToList();
                _mutableDict.Clear();
                OnChanged();
                return snapshot;
            }
        }

        public IReadOnlyCollection<TValue> Snapshot()
        {
            lock (this)
            {
                return Dictonary.Values.ToList();
            }
        }

        public void ForEach(Action<TKey, TValue> action)
        {
            lock (this)
            {
                foreach (var entry in _mutableDict)
                {
                    action(entry.Key, entry.Value);
                }
            }
        }

        public void ForEach(Action<TValue> action)
        {
            lock (this)
            {
                foreach (var value in _mutableDict.Values)
                {
                    action(value);
                }
            }
        }

        private void OnChanged()
        {
            _readonlyDict = null;
        }
    }
}
