using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization.Json;
using Xunit;

namespace SlimMessageBus.Host.Test
{
    public class RequestA : IRequestMessage<ResponseA>
    {
        public string Id { get; set; }

        public RequestA()
        {
            Id = Guid.NewGuid().ToString();
        }
    }

    public class ResponseA
    {
        public string Id { get; set; }
    }

    public class RequestB : IRequestMessage<ResponseB>
    {

    }

    public class ResponseB
    {
    }

    public class MessageBusBaseTest : IDisposable
    {
        private MessageBusTested _bus;
        private DateTimeOffset _timeZero;
        private DateTimeOffset _timeNow;

        private const int timeoutForA_10 = 10;
        private const int timeoutDefault_20 = 20;

        public MessageBusBaseTest()
        {
            _timeZero = DateTimeOffset.Now;
            _timeNow = _timeZero;

            var messageBusBuilder = new MessageBusBuilder()
                .Publish<RequestA>(x =>
                {
                    x.DefaultTopic("a-requests");
                    x.DefaultTimeout(TimeSpan.FromSeconds(timeoutForA_10));
                })
                .Publish<RequestB>(x =>
                {
                    x.DefaultTopic("b-requests");
                })
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic("app01-responses");
                    x.DefaultTimeout(TimeSpan.FromSeconds(timeoutDefault_20));
                })
                .WithDependencyResolver(new LookupDependencyResolver(t => null))
                .WithSerializer(new JsonMessageSerializer())
                .WithProvider(s => new MessageBusTested(s));

            _bus = (MessageBusTested)messageBusBuilder.Build();

            // provide current time
            _bus.CurrentTimeProvider = () => _timeNow;
        }

        public void Dispose()
        {
            _bus.Dispose();
        }

        [Fact]
        public void WhenNoTimeoutProvided_TakesDefaultTimeoutForRequestType()
        {
            // arrange
            var ra = new RequestA();
            var rb = new RequestB();

            // act
            var raTask = _bus.Send(ra);
            var rbTask = _bus.Send(rb);

            WaitForTasks(2000, raTask, rbTask);

            // after 10 seconds
            _timeNow = _timeZero.AddSeconds(timeoutForA_10 + 1);
            _bus.CleanPendingRequests();

            // assert
            raTask.IsCanceled.Should().BeTrue();
            rbTask.IsCanceled.Should().BeFalse();

            // adter 20 seconds
            _timeNow = _timeZero.AddSeconds(timeoutDefault_20 + 1);
            _bus.CleanPendingRequests();

            // assert
            rbTask.IsCanceled.Should().BeTrue();
        }

        [Fact]
        public void WhenResponseArrives_ResolvesPendingRequest()
        {
            // arrange
            var r = new RequestA();

            _bus.OnReply = (type, topic, request) =>
            {
                if (topic == "a-requests")
                {
                    var req = (RequestA)request;
                    return new ResponseA { Id = req.Id };
                }
                return null;
            };

            // act
            var rTask = _bus.Send(r);
            WaitForTasks(2000, rTask);
            _bus.CleanPendingRequests();

            // assert
            rTask.IsCompleted.Should().BeTrue("Response should be completed");
            r.Id.ShouldBeEquivalentTo(rTask.Result.Id);

            _bus.PendingRequestsCount.Should().Be(0, "There should be no pending requests");
        }

        [Fact]
        public void WhenResponseArrivesTooLate_ExpiresPendingRequest()
        {
            // arrange
            var r1 = new RequestA();
            var r2 = new RequestA();
            var r3 = new RequestA();

            _bus.OnReply = (type, topic, request) =>
            {
                if (topic == "a-requests")
                {
                    var req = (RequestA)request;
                    // resolve only r1 request
                    if (req.Id == r1.Id)
                    {
                        return new ResponseA { Id = req.Id };
                    }
                }
                return null;
            };

            // act
            var r1Task = _bus.Send(r1);
            var r2Task = _bus.Send(r2, TimeSpan.FromSeconds(1));
            var r3Task = _bus.Send(r3);

            // 2 seconds later
            _timeNow = _timeZero.AddSeconds(2);
            _bus.CleanPendingRequests();

            WaitForTasks(2000, r1Task, r2Task, r3Task);

            // assert
            r1Task.IsCompleted.Should().BeTrue("Response 1 should be completed");
            r2Task.IsCanceled.Should().BeTrue("Response 2 should be canceled");
            r3Task.IsCompleted.Should().BeFalse("Response 3 should still be pending");
            _bus.PendingRequestsCount.Should().Be(1, "There should be only 1 pending request");
        }

        [Fact]
        public void WhenCancellationTokenCancelled_CancellsPendingRequest()
        {
            // arrange
            var r1 = new RequestA();
            var r2 = new RequestA();

            var cts1 = new CancellationTokenSource();
            var cts2 = new CancellationTokenSource();

            var r1Task = _bus.Send(r1, cts1.Token);
            var r2Task = _bus.Send(r2, cts2.Token);

            // act
            cts2.Cancel();
            _bus.CleanPendingRequests();
            WaitForAnyTasks(2000, r1Task, r2Task);

            // assert
            r1Task.IsCompleted.Should().BeFalse("Request 1 is still pending");

            r2Task.IsCanceled.Should().BeTrue("Request 2 was canceled");
            r2Task.IsFaulted.Should().BeFalse("Request 2 was canceled");

            cts1.Dispose();
            cts2.Dispose();
        }

        private static void WaitForTasks(int millis, params Task[] tasks)
        {
            try
            {
                Task.WaitAll(tasks, millis);
            }
            catch (AggregateException)
            {
            }
        }

        private static void WaitForAnyTasks(int millis, params Task[] tasks)
        {
            try
            {
                Task.WaitAny(tasks, millis);
            }
            catch (AggregateException)
            {
            }
        }


        [Fact]
        public void SerializedRequestMessage_Property()
        {
            // arrange
            var r = new RequestA();
            var rid = "1";
            var replyTo = "some_topic";
            var expires = DateTimeOffset.UtcNow.AddMinutes(2);

            // act
            var p = _bus.SerializeRequest(typeof(RequestA), r, rid, replyTo, expires);

            _bus.DeserializeRequest(typeof(RequestA), p, out string rid2, out string replyTo2, out DateTimeOffset? expires2);

            // assert
            rid2.ShouldBeEquivalentTo(rid);
            replyTo2.ShouldBeEquivalentTo(replyTo);
            expires2.HasValue.Should().BeTrue();
            expires2.Value.Subtract(expires).TotalSeconds.Should().BeLessOrEqualTo(1);
        }
    }

    public class MessageBusTested : MessageBusBase
    {
        public MessageBusTested(MessageBusSettings settings) : base(settings)
        {
            // by default no responses will arrive
            OnReply = (type, payload, req) => null;
        }

        public int PendingRequestsCount => PendingRequestStore.GetCount();

        public Func<Type, string, object, object> OnReply { get; set; }

        #region Overrides of BaseMessageBus

        public override async Task Publish(Type messageType, byte[] payload, string topic)
        {
            var req = DeserializeRequest(messageType, payload, out string reqId, out string replyTo, out DateTimeOffset? expires);

            var resp = OnReply(messageType, topic, req);
            if (resp == null)
                return;

            var respPayload = SerializeResponse(resp.GetType(), resp, reqId, null);
            await OnResponseArrived(respPayload, replyTo);
        }

        #endregion

        #region Overrides of MessageBusBase

        public override DateTimeOffset CurrentTime => CurrentTimeProvider();

        #endregion

        public Func<DateTimeOffset> CurrentTimeProvider { get; set; }
    }
}
