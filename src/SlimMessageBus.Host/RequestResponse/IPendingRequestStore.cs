namespace SlimMessageBus.Host;

public interface IPendingRequestStore
{
    void Add(PendingRequestState requestState);
    bool Remove(string id);
    void RemoveAll(IEnumerable<string> ids);

    int GetCount();
    PendingRequestState GetById(string id);

    /// <summary>
    /// Find all the requests which either expired or cancellation was requested
    /// </summary>
    /// <param name="now"></param>
    /// <returns></returns>
    IReadOnlyCollection<PendingRequestState> FindAllToCancel(DateTimeOffset now);
}