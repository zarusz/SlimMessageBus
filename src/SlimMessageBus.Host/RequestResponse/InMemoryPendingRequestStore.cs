namespace SlimMessageBus.Host
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// In-memory and transient implementation of <see cref="IPendingRequestStore"/>.
    /// This class is thread-safe.
    /// </summary>
    public class InMemoryPendingRequestStore : IPendingRequestStore
    {
        private readonly object _itemsLock = new object();
        private readonly IDictionary<string, PendingRequestState> _items = new Dictionary<string, PendingRequestState>();

        #region Implementation of IPendingRequestsStore

        public void Add(PendingRequestState requestState)
        {
            Assert.IsNotNull(requestState, () => new ArgumentNullException(nameof(requestState)));

            lock (_itemsLock)
            {
                _items.Add(requestState.Id, requestState);
            }
        }

        public bool Remove(string id)
        {
            lock (_itemsLock)
            {
                return _items.Remove(id);
            }
        }

        public int GetCount()
        {
            lock (_itemsLock)
            {
                return _items.Count;
            }
        }

        public ICollection<PendingRequestState> FindAllToCancel(DateTimeOffset now)
        {
            lock (_itemsLock)
            {
                return _items.Values.Where(x => x.Expires < now || x.CancellationToken.IsCancellationRequested).ToList();
            }
        }

        public PendingRequestState GetById(string id)
        {
            lock (_itemsLock)
            {
                return _items.TryGetValue(id, out var requestState) ? requestState : null;
            }
        }

        #endregion
    }
}