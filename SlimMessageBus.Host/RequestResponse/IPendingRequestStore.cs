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

        /// <summary>
        /// Find all the requests which either expired or cancelation was requested
        /// </summary>
        /// <param name="now"></param>
        /// <returns></returns>
        ICollection<PendingRequestState> FindAllToCancel(DateTimeOffset now);
    }
}