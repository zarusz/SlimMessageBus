using System;
using System.Threading.Tasks;

namespace SlimMessageBus.Host
{
    public class PendingRequestMessageState
    {
        public string Id { get; }
        public object Request { get; }
        public Type ResponseType { get; }
        public TimeSpan Timeout { get; }
        public TaskCompletionSource<object> TaskCompletionSource { get; }

        public PendingRequestMessageState(string id, object request, Type responseType, TimeSpan timeout)
        {
            Id = id;
            Request = request;
            ResponseType = responseType;
            Timeout = timeout;
            // https://blogs.msdn.microsoft.com/pfxteam/2009/06/02/the-nature-of-taskcompletionsourcetresult/
            TaskCompletionSource = new TaskCompletionSource<object>();
        }
    }

    //public class PendingRequestMessageState<T>
    //{
    //    public TaskCompletionSource<T> TaskCompletionSource { get; }

    //    public PendingRequestMessageState(string id, object request, Type responseType, TimeSpan timeout)
    //    {
    //        Id = id;
    //        Request = request;
    //        ResponseType = responseType;
    //        Timeout = timeout;
    //        // https://blogs.msdn.microsoft.com/pfxteam/2009/06/02/the-nature-of-taskcompletionsourcetresult/
    //        TaskCompletionSource = new TaskCompletionSource<object>();
    //    }
    //}
}