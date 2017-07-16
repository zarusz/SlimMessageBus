using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Test
{
    [TestClass]
    public class ConsumerInstancePoolTest
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ConsumerInstancePoolTest));

        private MessageBusMock _busMock;

        [TestInitialize]
        public void Init()
        {
            _busMock = new MessageBusMock();
        }

        [TestMethod]
        public void AfterCreation_ResolvesNInstancesOfConsumer()
        {
            // arrange
            var consumerSettings = new ConsumerSettings
            {
                Instances = 2,
                ConsumerMode = ConsumerMode.Subscriber,
                ConsumerType = typeof(IConsumer<SomeMessage>),
                MessageType = typeof(SomeMessage)
            };

            // act
            var p = new ConsumerInstancePool<SomeMessage>(consumerSettings, _busMock.Object, x => new byte[0]);

            // assert
            _busMock.DependencyResolverMock.Verify(x => x.Resolve(typeof(IConsumer<SomeMessage>)), Times.Exactly(consumerSettings.Instances));
        }

        [TestMethod]
        public void AfterCreation_ResolvesNInstancesOfHandler()
        {
            // arrange
            var consumerSettings = new ConsumerSettings
            {
                Instances = 2,
                ConsumerMode = ConsumerMode.RequestResponse,
                ConsumerType = typeof(IRequestHandler<SomeRequest, SomeResponse>),
                MessageType = typeof(SomeRequest)
            };

            // act
            var p = new ConsumerInstancePool<SomeRequest>(consumerSettings, _busMock.Object, x => new byte[0]);

            // assert
            _busMock.DependencyResolverMock.Verify(x => x.Resolve(typeof(IRequestHandler<SomeRequest, SomeResponse>)), Times.Exactly(consumerSettings.Instances));
        }

        [TestMethod]
        public void Ensure_ExactlyNConsumerInstancesAreWorking()
        {
            const int consumerTime = 500;
            const int consumerInstances = 8;
            const int messageCount = 500;

            // arrange
            var consumerSettings = new ConsumerSettings
            {
                Instances = consumerInstances,
                ConsumerMode = ConsumerMode.Subscriber,
                ConsumerType = typeof(IConsumer<SomeMessage>),
                MessageType = typeof(SomeMessage)
            };

            var activeInstances = 0;
            var processedMessageCount = 0;

            var maxInstances = 0;
            var maxInstancesLock = new object();

            _busMock.ConsumerMock.Setup(x => x.OnHandle(It.IsAny<SomeMessage>(), It.IsAny<string>()))
                .Returns((SomeMessage msg, string topic) =>
                {
                    Interlocked.Increment(ref activeInstances);
                    lock (maxInstancesLock)
                    {
                        // capture maximum active 
                        maxInstances = Math.Max(maxInstances, activeInstances);
                    }
                    return Task
                        .Delay(consumerTime)
                        .ContinueWith(t =>
                        {
                            Interlocked.Decrement(ref activeInstances);
                            Interlocked.Increment(ref processedMessageCount);
                        });
                });

            var p = new ConsumerInstancePool<SomeMessage>(consumerSettings, _busMock.Object, x => new byte[0]);

            // act
            var time = Stopwatch.StartNew();
            var tasks = new Task[messageCount];
            for (var i = 0; i < messageCount; i++)
            {
                tasks[i] = p.ProcessMessage(new SomeMessage());
            }
            Task.WaitAll(tasks);
            // this is slower
            //Parallel.For(0, messageCount, i => p.ProcessMessage(new SomeMessage()).Wait());            
            
            time.Stop();

            // assert
            var minPossibleTime = messageCount * consumerTime / consumerSettings.Instances;
            var maxPossibleTime = messageCount * consumerTime;

            processedMessageCount.ShouldBeEquivalentTo(messageCount);
            maxInstances.ShouldBeEquivalentTo(consumerSettings.Instances);
            // max concurrent consumers should reach number of desired instances
            time.ElapsedMilliseconds.Should().BeInRange(minPossibleTime, maxPossibleTime);

            var percent = Math.Round(100f * (time.ElapsedMilliseconds - minPossibleTime) / (maxPossibleTime - minPossibleTime), 2);
            Log.InfoFormat("The execution time was {0}% away from the best possible time", percent); // smallest number is better
        }

        [TestMethod]
        public void WhenRequestExpired_OnMessageExpired_IsCalled()
        {
            // arrange

            var onMessageExpiredMock = new Mock<Action<ConsumerSettings, object>>();
            var consumerSettings = new ConsumerSettings
            {
                Instances = 1,
                Topic = "topic1",
                ConsumerMode = ConsumerMode.RequestResponse,
                ConsumerType = typeof(IRequestHandler<SomeRequest, SomeResponse>),
                MessageType = typeof(SomeRequest),
                OnMessageExpired = onMessageExpiredMock.Object
            };

            var p = new ConsumerInstancePool<SomeRequest>(consumerSettings, _busMock.Object, x => new byte[0]);

            var request = new SomeRequest();
            string requestId;
            string replyTo;
            DateTimeOffset? expires = _busMock.CurrentTime.AddSeconds(-10);
            _busMock.BusMock.Setup(x => x.DeserializeRequest(typeof(SomeRequest), It.IsAny<byte[]>(), out requestId, out replyTo, out expires)).Returns(request);

            // act
            p.ProcessMessage(request).Wait();

            // assert
            _busMock.HandlerMock.Verify(x => x.OnHandle(It.IsAny<SomeRequest>(), It.IsAny<string>()), Times.Never); // the handler should not be called

            onMessageExpiredMock.Verify(x => x(consumerSettings, request), Times.Once); // callback called once
        }

        [TestMethod]
        public void WhenRequestFails_OnMessageFault_IsCalled_And_ErrorResponse_IsSent()
        {
            // arrange
            var onMessageFaultMock = new Mock<Action<ConsumerSettings, object, Exception>>();
            var consumerSettings = new ConsumerSettings
            {
                Instances = 1,
                Topic = "topic1",
                ConsumerMode = ConsumerMode.RequestResponse,
                ConsumerType = typeof(IRequestHandler<SomeRequest, SomeResponse>),
                MessageType = typeof(SomeRequest),
                OnMessageFault = onMessageFaultMock.Object
            };

            var p = new ConsumerInstancePool<SomeRequest>(consumerSettings, _busMock.Object, x => new byte[0]);

            var request = new SomeRequest();
            string requestId;
            string replyTo = "reply-topic";
            DateTimeOffset? expires;
            _busMock.BusMock.Setup(x => x.DeserializeRequest(typeof(SomeRequest), It.IsAny<byte[]>(), out requestId, out replyTo, out expires)).Returns(request);

            var ex = new Exception("Something went bad");
            _busMock.HandlerMock.Setup(x => x.OnHandle(request, consumerSettings.Topic)).Returns(Task.FromException<SomeResponse>(ex));

            // act
            p.ProcessMessage(request).Wait();

            // assert
            _busMock.HandlerMock.Verify(x => x.OnHandle(request, consumerSettings.Topic), Times.Once); // handler called once

            onMessageFaultMock.Verify(x => x(consumerSettings, request, ex), Times.Once); // callback called once
            _busMock.BusMock.Verify(x => x.Publish(typeof(SomeResponse), It.IsAny<byte[]>(), replyTo));
        }

        [TestMethod]
        public void WhenMessageFails_OnMessageFault_IsCalled()
        {
            // arrange
            var onMessageFaultMock = new Mock<Action<ConsumerSettings, object, Exception>>();
            var consumerSettings = new ConsumerSettings
            {
                Instances = 1,
                Topic = "topic1",
                ConsumerMode = ConsumerMode.Subscriber,
                ConsumerType = typeof(IConsumer<SomeMessage>),
                MessageType = typeof(SomeMessage),
                OnMessageFault = onMessageFaultMock.Object
            };

            var p = new ConsumerInstancePool<SomeMessage>(consumerSettings, _busMock.Object, x => new byte[0]);

            var message = new SomeMessage();
            _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeMessage), It.IsAny<byte[]>())).Returns(message);

            var ex = new Exception("Something went bad");
            _busMock.ConsumerMock.Setup(x => x.OnHandle(message, consumerSettings.Topic)).Returns(Task.FromException<SomeResponse>(ex));

            // act
            p.ProcessMessage(message).Wait();

            // assert
            _busMock.ConsumerMock.Verify(x => x.OnHandle(message, consumerSettings.Topic), Times.Once); // handler called once

            onMessageFaultMock.Verify(x => x(consumerSettings, message, ex), Times.Once); // callback called once
        }
    }
}
