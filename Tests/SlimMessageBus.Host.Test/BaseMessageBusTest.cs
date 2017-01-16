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
    public class BaseMessageBusTest
    {
        private MessageBusBusTested _busBus;

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
                .WithProvider(s => new MessageBusBusTested(s));

            _busBus = (MessageBusBusTested)messageBusBuilder.Build();
        }

        [TestMethod]
        public void WhenResponseArrives_ResolvesPendingRequest()
        {
            // arrange
            var r1 = new RequestA();
            var r2 = new RequestA();

            _busBus.OnReply = (type, topic, request) =>
            {
                if (topic == "a-requests")
                {
                    var req = (RequestA) request;
                    return new ResponseA {Id = req.Id};
                }
                return null;
            };

            // act
            var r1Task = _busBus.Send(r1);
            var r2Task = _busBus.Send(r2);

            Task.WaitAll(new Task[] {r1Task, r2Task}, 3000);

            // assert
            Assert.IsTrue(r1Task.IsCompleted, "Response 1 should be completed");
            Assert.AreEqual(r1.Id, r1Task.Result.Id);

            Assert.IsTrue(r2Task.IsCompleted, "Response 2 should be completed");
            Assert.AreEqual(r2.Id, r2Task.Result.Id);

            Assert.AreEqual(0, _busBus.PendingRequestsCount, "There should be no pending requests");
        }


        [TestMethod]
        public void WhenResponseArrivesTooLate_ExpiresPendingRequest()
        {
            // arrange
            var r1 = new RequestA();
            var r2 = new RequestA();
            var r3 = new RequestA();

            _busBus.OnReply = (type, topic, request) =>
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
            var r1Task = _busBus.Send(r1);
            var r2Task = _busBus.Send(r2, TimeSpan.FromSeconds(1));
            var r3Task = _busBus.Send(r3);

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

            Assert.AreEqual(1, _busBus.PendingRequestsCount, "There should be only 1 pending request");
        }
    }


    public class MessageBusBusTested : MessageBusBus
    {
        public MessageBusBusTested(MessageBusSettings settings) : base(settings)
        {
        }

        public int PendingRequestsCount => PendingRequests.Count;

        public Func<Type, string, object, object> OnReply { get; set; }

        #region Overrides of BaseMessageBus

        protected override Task Publish(Type type, string topic, byte[] payload)
        {
            // async execution (no wait)
            Task.Run(() =>
            {
                var reqH = (MessageWithHeaders)Settings.RequestResponse.MessageWithHeadersSerializer.Deserialize(typeof(MessageWithHeaders), payload);
                var reqId = reqH.Headers[MessageBusBus.HeaderRequestId];
                var reqReplyTo = reqH.Headers[MessageBusBus.HeaderReplyTo];
                var req = Settings.Serializer.Deserialize(type, reqH.Payload);

                var resp = OnReply(type, topic, req);
                if (resp == null)
                    return;

                var respPayload = Settings.Serializer.Serialize(resp.GetType(), resp);
                var respH = new MessageWithHeaders(respPayload);
                respH.Headers.Add(MessageBusBus.HeaderRequestId, reqId);
                var respHPayload = Settings.RequestResponse.MessageWithHeadersSerializer.Serialize(typeof(MessageWithHeaders), respH);

                OnResponseArrived(respHPayload, reqReplyTo).Wait();
            });

            return Task.FromResult(0);
        }

        #endregion
    }
}
