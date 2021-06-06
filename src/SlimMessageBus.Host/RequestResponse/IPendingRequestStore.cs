namespace SlimMessageBus.Host
{
    using System;
    using System.Collections.Generic;

    public interface IPendingRequestStore
    {
        void Add(PendingRequestState requestState);
        bool Remove(string id);

        int GetCount();
        PendingRequestState GetById(string id);

        /// <summary>
        /// Find all the requests which either expired or cancellation was requested
        /// </summary>
        /// <param name="now"></param>
        /// <returns></returns>
        ICollection<PendingRequestState> FindAllToCancel(DateTimeOffset now);
    }
}