using System;
using System.Collections.Concurrent;
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

    public class BaseMessageBusTested : BaseMessageBus
    {
        public BaseMessageBusTested(MessageBusSettings settings) : base(settings)
        {
        }

        #region Overrides of BaseMessageBus

        protected override Task Publish(Type type, string topic, byte[] payload)
        {
            if (topic == "a-requests")
            {
                // async execution (no wait)
                Task.Run(() =>
                {
                    var reqH = (MessageWithHeaders)Settings.RequestResponse.MessageWithHeadersSerializer.Deserialize(typeof(MessageWithHeaders), payload);
                    var reqId = reqH.Headers[BaseMessageBus.HeaderRequestId];
                    var reqReplyTo = reqH.Headers[BaseMessageBus.HeaderReplyTo];
                    var req = (RequestA)Settings.Serializer.Deserialize(typeof(RequestA), reqH.Payload);

                    var resp = new ResponseA { Id = req.Id };
                    var respPayload = Settings.Serializer.Serialize(typeof(ResponseA), resp);
                    var respH = new MessageWithHeaders(respPayload);
                    respH.Headers.Add(BaseMessageBus.HeaderRequestId, reqId);
                    var respHPayload = Settings.RequestResponse.MessageWithHeadersSerializer.Serialize(typeof(MessageWithHeaders), respH);

                    OnResponseArrived(respHPayload, reqReplyTo).Wait();
                });
            }

            return Task.FromResult(0);
        }

        #endregion
    }

    [TestClass]
    public class BaseMessageBusTest
    {
        private BaseMessageBus _bus;

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
                //.WithSubscriberResolver(new FakeDependencyResolver(_pingSubscriber))
                //.WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers));
                .WithProvider(s => new BaseMessageBusTested(s));

            _bus = (BaseMessageBus) messageBusBuilder.Build();
        }

        [TestMethod]
        public async Task ResolvesRequestWhenResponseArrives()
        {
            // arrange
            var a1 = new RequestA { Id = Guid.NewGuid().ToString() };
            var a2 = new RequestA { Id = Guid.NewGuid().ToString() };
            var a3 = new RequestA { Id = Guid.NewGuid().ToString() };

            // act
            var ra1Task = _bus.Request(a1);
            var ra2Task = _bus.Request(a2);
            var ra3Task = _bus.Request(a3);

            Task.WaitAll(new Task[] { ra1Task, ra2Task, ra3Task }, 5000);

            // assert
            Assert.IsTrue(ra1Task.IsCompleted, "Response 1 should be completed by now");
            Assert.IsTrue(ra2Task.IsCompleted, "Response 2 should be completed by now");
            Assert.IsTrue(ra3Task.IsCompleted, "Response 3 should be completed by now");

            Assert.AreEqual(a1.Id, ra1Task.Result.Id);
            Assert.AreEqual(a2.Id, ra2Task.Result.Id);
            Assert.AreEqual(a3.Id, ra3Task.Result.Id);
        }

    }
}
