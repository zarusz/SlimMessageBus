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
    }

    public class ResponseA
    {
        public string Id { get; set; }
    }

    [TestClass]
    public class BaseMessageBusTest
    {
        private BaseMessageBusTested _bus;

        [TestInitialize]
        public void Init()
        {
            var messageBusBuilder = new MessageBusBuilder()
                .Publish<RequestA>(x =>
                {
                    x.OnTopicByDefault("a-requests");
                })
                .ExpectRequestResponses(x =>
                {
                    x.OnTopic("app01-responses");
                    x.DefaultTimeout(TimeSpan.FromSeconds(10));
                })
                .WithSerializer(new JsonMessageSerializer())
                .WithProvider(s => new BaseMessageBusTested(s));

            _bus = (BaseMessageBusTested) messageBusBuilder.Build();
        }

        [TestMethod]
        public void WhenResponseArrives_ResolvesPendingRequest()
        {
            // arrange
            var r1 = new RequestA { Id = Guid.NewGuid().ToString() };
            var r2 = new RequestA { Id = Guid.NewGuid().ToString() };
            var r3 = new RequestA { Id = Guid.NewGuid().ToString() };

            _bus.OnReply = (type, topic, request) =>
            {
                if (topic == "a-requests")
                {
                    var req = (RequestA) request;
                    // do not resolve a2 request
                    if (req.Id != r2.Id)
                    {
                        return new ResponseA { Id = req.Id };
                    }
                }
                return null;
            };

            // act
            var r1Task = _bus.Request(r1);
            var r2Task = _bus.Request(r2);
            var r3Task = _bus.Request(r3);

            Task.WaitAll(new Task[] { r1Task, r2Task, r3Task }, 5000);

            // assert
            Assert.IsTrue(r1Task.IsCompleted, "Response 1 should be completed by now");
            Assert.IsFalse(r2Task.IsCompleted, "Response 2 should not complete");
            Assert.IsTrue(r3Task.IsCompleted, "Response 3 should be completed by now");

            Assert.AreEqual(r1.Id, r1Task.Result.Id);
            Assert.AreEqual(r3.Id, r3Task.Result.Id);
        }

    }

    public class BaseMessageBusTested : BaseMessageBus
    {
        public BaseMessageBusTested(MessageBusSettings settings) : base(settings)
        {
        }

        public Func<Type, string, object, object> OnReply { get; set; }

        #region Overrides of BaseMessageBus

        protected override Task Publish(Type type, string topic, byte[] payload)
        {
            // async execution (no wait)
            Task.Run(() =>
            {
                var reqH = (MessageWithHeaders)Settings.RequestResponse.MessageWithHeadersSerializer.Deserialize(typeof(MessageWithHeaders), payload);
                var reqId = reqH.Headers[BaseMessageBus.HeaderRequestId];
                var reqReplyTo = reqH.Headers[BaseMessageBus.HeaderReplyTo];
                var req = Settings.Serializer.Deserialize(type, reqH.Payload);

                var resp = OnReply(type, topic, req);
                if (resp == null)
                    return;

                var respPayload = Settings.Serializer.Serialize(resp.GetType(), resp);
                var respH = new MessageWithHeaders(respPayload);
                respH.Headers.Add(BaseMessageBus.HeaderRequestId, reqId);
                var respHPayload = Settings.RequestResponse.MessageWithHeadersSerializer.Serialize(typeof(MessageWithHeaders), respH);

                OnResponseArrived(respHPayload, reqReplyTo).Wait();
            });

            return Task.FromResult(0);
        }

        #endregion
    }
}
