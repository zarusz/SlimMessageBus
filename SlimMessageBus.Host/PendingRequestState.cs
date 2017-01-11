using System;
using System.Threading.Tasks;

namespace SlimMessageBus.Host
{
    public class PendingRequestState
    {
        public string Id { get; }
        public object Request { get; }
        public Type RequestType { get; }
        public Type ResponseType { get; }
        public DateTime Created { get; }
        public TimeSpan Timeout { get; }
        public TaskCompletionSource<object> TaskCompletionSource { get; }

        public PendingRequestState(string id, object request, Type requestType, Type responseType, TimeSpan timeout)
        {
            Id = id;
            Request = request;
            RequestType = requestType;
            ResponseType = responseType;
            Created = DateTime.Now;
            Timeout = timeout;
            // https://blogs.msdn.microsoft.com/pfxteam/2009/06/02/the-nature-of-taskcompletionsourcetresult/
            TaskCompletionSource = new TaskCompletionSource<object>();
        }
    }
}