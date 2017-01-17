using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization.Json;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

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
                })
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic("app01-responses");
                    x.DefaultTimeout(TimeSpan.FromSeconds(20));
                })
                .WithSerializer(new JsonMessageSerializer())
                .WithProvider(s => new MessageBusTested(s));

            _bus = (MessageBusTested)messageBusBuilder.Build();
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
                    var req = (RequestA) request;
                    return new ResponseA {Id = req.Id};
                }
                return null;
            };

            // act
            var r1Task = _bus.Send(r1);
            var r2Task = _bus.Send(r2);

            Task.WaitAll(new Task[] {r1Task, r2Task}, 3000);

            // assert
            Assert.IsTrue(r1Task.IsCompleted, "Response 1 should be completed");
            Assert.AreEqual(r1.Id, r1Task.Result.Id);

            Assert.IsTrue(r2Task.IsCompleted, "Response 2 should be completed");
            Assert.AreEqual(r2.Id, r2Task.Result.Id);

            Assert.AreEqual(0, _bus.PendingRequestsCount, "There should be no pending requests");
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
            Assert.IsTrue(r1Task.IsCompleted, "Response 1 should be completed");
            Assert.IsTrue(r2Task.IsCanceled, "Response 2 should be canceled");
            Assert.IsTrue(!r3Task.IsCanceled && !r3Task.IsCompleted, "Response 3 should still be pending");

            Assert.AreEqual(1, _bus.PendingRequestsCount, "There should be only 1 pending request");
        }
    }


    public class MessageBusTested : MessageBusBase
    {
        public MessageBusTested(MessageBusSettings settings) : base(settings)
        {
        }

        public int PendingRequestsCount => PendingRequests.Count;

        public Func<Type, string, object, object> OnReply { get; set; }

        #region Overrides of BaseMessageBus

        public override Task Publish(Type messageType, byte[] payload, string topic)
        {
            // async execution (no wait)
            Task.Run(() =>
            {
                string reqId, replyTo;
                var req = DeserializeRequest(messageType, payload, out reqId, out replyTo);

                var resp = OnReply(messageType, topic, req);
                if (resp == null)
                    return;

                var respPayload = SerializeResponse(resp.GetType(), resp, reqId);
                OnResponseArrived(respPayload, replyTo).Wait();
            });

            return Task.FromResult(0);
        }

        #endregion
    }
}
