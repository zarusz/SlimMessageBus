using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization.Json;

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

    [TestClass]
    public class MessageBusBaseTest
    {
        private MessageBusTested _bus;

        [TestInitialize]
        public void Init()
        {
            var messageBusBuilder = new MessageBusBuilder()
                .Publish<RequestA>(x =>
                {
                    x.DefaultTopic("a-requests");
                    x.DefaultTimeout(TimeSpan.FromSeconds(10));
                })
                .Publish<RequestB>(x =>
                {
                    x.DefaultTopic("b-requests");
                })
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic("app01-responses");
                    x.DefaultTimeout(TimeSpan.FromSeconds(20));
                })
                .WithSerializer(new JsonMessageSerializer())
                .WithProvider(s => new MessageBusTested(s));

            _bus = (MessageBusTested)messageBusBuilder.Build();

            // provide current time
            _bus.CurrentTimeProvider = () => DateTimeOffset.UtcNow;
        }

        [TestCleanup]
        public void Cleanup()
        {
            _bus.Dispose();
        }

        [TestMethod]
        public void WhenNoTimeoutProvided_TakesDefault()
        {
            // arrange
            var ra = new RequestA();
            var rb = new RequestB();
            var t0 = DateTimeOffset.UtcNow;
            var t10 = t0.AddSeconds(11);
            var t20 = t0.AddSeconds(21);

            // act
            var raTask = _bus.Send(ra);
            var rbTask = _bus.Send(rb);

            // when 10 seconds passed
            _bus.CurrentTimeProvider = () => t10;
            Thread.Sleep(1200); // give the internal cleanup timer a chance to execute

            // assert
            raTask.IsCanceled.Should().BeTrue();
            rbTask.IsCanceled.Should().BeFalse();

            // when 20 seconds passed
            _bus.CurrentTimeProvider = () => t20;
            Thread.Sleep(1200); // give the internal cleanup timer a chance to execute

            rbTask.IsCanceled.Should().BeTrue();
        }

        [TestMethod]
        public void WhenResponseArrives_ResolvesPendingRequest()
        {
            // arrange
            var r1 = new RequestA();
            var r2 = new RequestA();

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
            var r1Task = _bus.Send(r1);
            var r2Task = _bus.Send(r2);

            Task.WaitAll(new Task[] { r1Task, r2Task }, 3000);

            // assert
            r1Task.IsCompleted.Should().BeTrue("Response 1 should be completed");
            r1.Id.ShouldBeEquivalentTo(r1Task.Result.Id);

            r2Task.IsCompleted.Should().BeTrue("Response 2 should be completed");
            r2.Id.ShouldBeEquivalentTo(r2Task.Result.Id);

            _bus.PendingRequestsCount.Should().Be(0, "There should be no pending requests");
        }


        [TestMethod]
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

            try
            {
                Task.WaitAll(new Task[] { r1Task, r2Task, r3Task }, 3000);
            }
            catch (AggregateException)
            {
            }

            // assert
            r1Task.IsCompleted.Should().BeTrue("Response 1 should be completed");
            r2Task.IsCanceled.Should().BeTrue("Response 2 should be canceled");
            (!r3Task.IsCanceled && !r3Task.IsCompleted).Should().BeTrue("Response 3 should still be pending");
            _bus.PendingRequestsCount.Should().Be(1, "There should be only 1 pending request");
        }

        [TestMethod]
        public void WhenCancellationTokenCancelled_CancellsPendingRequest()
        {
            // arrange
            var r1 = new RequestA();
            var r2 = new RequestA();
            
            _bus.OnReply = (type, topic, request) => null; // none request will be processed

            var cts1 = new CancellationTokenSource();
            var cts2 = new CancellationTokenSource();

            var r1Task = _bus.Send(r1, cts1.Token);
            var r2Task = _bus.Send(r2, cts2.Token);

            // act
            cts2.Cancel();

            // assert
            Thread.Sleep(1200); // wait until internal timer has enough time to sweep the pending requests

            r1Task.IsCanceled.Should().BeFalse();
            r1Task.IsFaulted.Should().BeFalse();
            r1Task.IsCompleted.Should().BeFalse();

            r2Task.IsCanceled.Should().BeTrue();
            r2Task.IsFaulted.Should().BeFalse();
            r2Task.IsCompleted.Should().BeTrue();

            cts1.Dispose();
            cts2.Dispose();
        }

        [TestMethod]
        public void SerializedRequestMessage_Property()
        {
            // arrange
            var r = new RequestA();
            var rid = "1";
            var replyTo = "some_topic";
            var expires = DateTimeOffset.UtcNow.AddMinutes(2);

            // act
            var p = _bus.SerializeRequest(typeof(RequestA), r, rid, replyTo, expires);

            string rid2;
            string replyTo2;
            DateTimeOffset? expires2;
            _bus.DeserializeRequest(typeof (RequestA), p, out rid2, out replyTo2, out expires2);

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
        }

        public int PendingRequestsCount => PendingRequestStore.GetCount();

        public Func<Type, string, object, object> OnReply { get; set; }

        #region Overrides of BaseMessageBus

        public override Task Publish(Type messageType, byte[] payload, string topic)
        {
            // async execution (no wait)
            Task.Run(() =>
            {
                string reqId, replyTo;
                DateTimeOffset? expires;
                var req = DeserializeRequest(messageType, payload, out reqId, out replyTo, out expires);

                var resp = OnReply(messageType, topic, req);
                if (resp == null)
                    return;

                var respPayload = SerializeResponse(resp.GetType(), resp, reqId, null);
                OnResponseArrived(respPayload, replyTo).Wait();
            });

            return Task.FromResult(0);
        }

        #endregion

        #region Overrides of MessageBusBase

        public override DateTimeOffset CurrentTime => CurrentTimeProvider();

        #endregion

        public Func<DateTimeOffset> CurrentTimeProvider { get; set; }
    }
}
