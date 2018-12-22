using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.DependencyResolver;
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
        private readonly MessageBusTested _bus;
        private readonly DateTimeOffset _timeZero;
        private DateTimeOffset _timeNow;

        private const int TimeoutForA10 = 10;
        private const int TimeoutDefault20 = 20;

        public MessageBusBaseTest()
        {
            _timeZero = DateTimeOffset.Now;
            _timeNow = _timeZero;

            var messageBusBuilder = MessageBusBuilder.Create()
                .Produce<RequestA>(x =>
                {
                    x.DefaultTopic("a-requests");
                    x.DefaultTimeout(TimeSpan.FromSeconds(TimeoutForA10));
                })
                .Produce<RequestB>(x =>
                {
                    x.DefaultTopic("b-requests");
                })
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic("app01-responses");
                    x.DefaultTimeout(TimeSpan.FromSeconds(TimeoutDefault20));
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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _bus.Dispose();
            }
        }

        [Fact]
        public void WhenNoTimeoutProvidedThenTakesDefaultTimeoutForRequestType()
        {
            // arrange
            var ra = new RequestA();
            var rb = new RequestB();

            // act
            var raTask = _bus.Send(ra);
            var rbTask = _bus.Send(rb);

            WaitForTasks(2000, raTask, rbTask);

            // after 10 seconds
            _timeNow = _timeZero.AddSeconds(TimeoutForA10 + 1);
            _bus.TriggerPendingRequestCleanup();

            // assert
            raTask.IsCanceled.Should().BeTrue();
            rbTask.IsCanceled.Should().BeFalse();

            // adter 20 seconds
            _timeNow = _timeZero.AddSeconds(TimeoutDefault20 + 1);
            _bus.TriggerPendingRequestCleanup();

            // assert
            rbTask.IsCanceled.Should().BeTrue();
        }

        [Fact]
        public void WhenResponseArrivesThenResolvesPendingRequest()
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
            _bus.TriggerPendingRequestCleanup();

            // assert
            rTask.IsCompleted.Should().BeTrue("Response should be completed");
            r.Id.Should().Be(rTask.Result.Id);

            _bus.PendingRequestsCount.Should().Be(0, "There should be no pending requests");
        }

        [Fact]
        public void WhenResponseArrivesTooLateThenExpiresPendingRequest()
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
            _bus.TriggerPendingRequestCleanup();

            WaitForTasks(2000, r1Task, r2Task, r3Task);

            // assert
            r1Task.IsCompleted.Should().BeTrue("Response 1 should be completed");
            r2Task.IsCanceled.Should().BeTrue("Response 2 should be canceled");
            r3Task.IsCompleted.Should().BeFalse("Response 3 should still be pending");
            _bus.PendingRequestsCount.Should().Be(1, "There should be only 1 pending request");
        }

        [Fact]
        public void WhenCancellationTokenCancelledThenCancellsPendingRequest()
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
            _bus.TriggerPendingRequestCleanup();
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
                // swallow
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
                // swallow
            }
        }


        [Fact]
        public void WhenRequestMessageSerializedThenDeserializeGivesSameObject()
        {
            // arrange
            var r = new RequestA();
            var rid = "1";
            var replyTo = "some_topic";
            var expires = DateTimeOffset.UtcNow.AddMinutes(2);

            // act
            var payload = _bus.SerializeRequest(typeof(RequestA), r, rid, replyTo, expires);

            _bus.DeserializeRequest(typeof(RequestA), payload, out var rid2, out var replyTo2, out var expires2);

            // assert
            rid2.Should().Be(rid);
            replyTo2.Should().Be(replyTo);
            expires2.Should().HaveValue();
            expires2.Value.EqualsExact(expires);
        }

        [Fact]
        public void GivenDisposedWhenPublishThenThrowsException()
        {
            // arrange
            _bus.Dispose();

            // act
            Func<Task> act = async () => await _bus.Publish(new SomeMessage()).ConfigureAwait(false);
            Func<Task> actWithTopic = async () => await _bus.Publish(new SomeMessage(), "some-topic").ConfigureAwait(false);

            // assert
            act.Should().Throw<MessageBusException>();
            actWithTopic.Should().Throw<MessageBusException>();
        }

        [Fact]
        public void GivenDisposedWhenSendThenThrowsException()
        {
            // arrange
            _bus.Dispose();

            // act
            Func<Task> act = async () => await _bus.Send(new SomeRequest()).ConfigureAwait(false);
            Func<Task> actWithTopic = async () => await _bus.Send(new SomeRequest(), "some-topic").ConfigureAwait(false);

            // assert
            act.Should().Throw<MessageBusException>();
            actWithTopic.Should().Throw<MessageBusException>();
        }

    }

    public class MessageBusTested : MessageBusBase
    {
        public MessageBusTested(MessageBusSettings settings) 
            : base(settings)
        {
            // by default no responses will arrive
            OnReply = (type, payload, req) => null;
        }

        public int PendingRequestsCount => PendingRequestStore.GetCount();

        public Func<Type, string, object, object> OnReply { get; set; }

        #region Overrides of BaseMessageBus

        public override Task PublishToTransport(Type messageType, object message, string topic, byte[] payload)
        {
            var req = DeserializeRequest(messageType, payload, out var reqId, out var replyTo, out var expires);

            var resp = OnReply(messageType, topic, req);
            if (resp == null)
            {
                return Task.CompletedTask;
            }

            var respPayload = SerializeResponse(resp.GetType(), resp, reqId, null);
            return OnResponseArrived(respPayload, replyTo);
        }

        #endregion

        #region Overrides of MessageBusBase

        public override DateTimeOffset CurrentTime => CurrentTimeProvider();

        #endregion

        public Func<DateTimeOffset> CurrentTimeProvider { get; set; }

        public void TriggerPendingRequestCleanup()
        {
            PendingRequestManager.CleanPendingRequests();
        }
    }
}
