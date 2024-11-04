namespace SlimMessageBus.Host;

public interface IPendingRequestManager
{
    IPendingRequestStore Store { get; }
    void CleanPendingRequests();
}