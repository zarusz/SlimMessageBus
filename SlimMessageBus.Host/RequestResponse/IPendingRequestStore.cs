using System;
using System.Collections.Generic;

namespace SlimMessageBus.Host
{
    public interface IPendingRequestStore
    {
        void Add(PendingRequestState requestState);
        bool Remove(string id);

        int GetCount();
        PendingRequestState GetById(string id);
        ICollection<PendingRequestState> GetAllExpired(DateTimeOffset now);
    }
}