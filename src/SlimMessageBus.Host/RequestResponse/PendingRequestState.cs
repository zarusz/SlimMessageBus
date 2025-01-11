namespace SlimMessageBus.Host;

public class PendingRequestState
{
    public string Id { get; }
    public object Request { get; }
    public Type RequestType { get; }
    public Type ResponseType { get; }
    public DateTimeOffset Created { get; }
    public DateTimeOffset Expires { get; }
    public TaskCompletionSource<object> TaskCompletionSource { get; }
    public CancellationToken CancellationToken { get; }

    public PendingRequestState(string id, object request, Type requestType, Type responseType, DateTimeOffset created, DateTimeOffset expires, CancellationToken cancellationToken)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Request = request ?? throw new ArgumentNullException(nameof(request));
        RequestType = requestType ?? throw new ArgumentNullException(nameof(requestType));
        ResponseType = responseType ?? throw new ArgumentNullException(nameof(responseType));
        Created = created;
        Expires = expires;
        // https://blogs.msdn.microsoft.com/pfxteam/2009/06/02/the-nature-of-taskcompletionsourcetresult/
        TaskCompletionSource = new TaskCompletionSource<object>();
        CancellationToken = cancellationToken;
    }

    public override string ToString() => $"Request(Id: {Id}, RequestType: {RequestType}, ResponseType: {ResponseType}, Created: {Created}, Expires: {Expires})";
}