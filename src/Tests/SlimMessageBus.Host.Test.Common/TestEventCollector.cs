namespace SlimMessageBus.Host.Test.Common
{
    using System.Collections.Generic;

    public class TestEventCollector<T>
    {
        private readonly IList<T> list = new List<T>();

        private bool isStarted = false;

        public bool IsStarted => isStarted;

        public void Add(T item)
        {
            lock (list)
            {
                list.Add(item);
            }
        }

        public IList<T> Snapshot()
        {
            lock (list)
            {
                var snapshot = new List<T>(list);
                return snapshot;
            }
        }

        public void Start()
        {
            isStarted = true;
        }

        public int Count
        {
            get
            {
                lock (list)
                {
                    return list.Count;
                }
            }
        }

        public void Clear()
        {
            lock (list)
            {
                list.Clear();
            }
        }
    }
}