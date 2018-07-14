using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using FluentAssertions;
using Moq;
using SlimMessageBus.Host.Config;
using Xunit;

namespace SlimMessageBus.Host.Test
{
    public class ConsumerInstancePoolTest
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly MessageBusMock _busMock;

        public ConsumerInstancePoolTest()
        {
            _busMock = new MessageBusMock();
        }

        [Fact]
        public void WhenNewInstanceThenResolvesNInstancesOfConsumer()
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
            var p = new ConsumerInstancePool<SomeMessage>(consumerSettings, _busMock.Bus, x => Array.Empty<byte>());

            // assert
            _busMock.DependencyResolverMock.Verify(x => x.Resolve(typeof(IConsumer<SomeMessage>)), Times.Exactly(consumerSettings.Instances));
        }

        [Fact]
        public void WhenNewInstanceThenResolvesNInstancesOfHandler()
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
            var p = new ConsumerInstancePool<SomeRequest>(consumerSettings, _busMock.Bus, x => Array.Empty<byte>());

            // assert
            _busMock.DependencyResolverMock.Verify(x => x.Resolve(typeof(IRequestHandler<SomeRequest, SomeResponse>)),
                Times.Exactly(consumerSettings.Instances));
        }

        [Fact]
        public void WhenNInstancesConfiguredThenExactlyNConsumerInstancesAreWorking()
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
                        }, TaskScheduler.Current);
                });

            var p = new ConsumerInstancePool<SomeMessage>(consumerSettings, _busMock.Bus, x => Array.Empty<byte>());

            // act
            var time = Stopwatch.StartNew();
            var tasks = new Task[messageCount];
            for (var i = 0; i < messageCount; i++)
            {
                tasks[i] = p.ProcessMessage(new SomeMessage());
            }

            Task.WaitAll(tasks);

            time.Stop();

            // assert
            var minPossibleTime = messageCount * consumerTime / consumerSettings.Instances;
            var maxPossibleTime = messageCount * consumerTime;

            processedMessageCount.Should().Be(messageCount);
            maxInstances.Should().Be(consumerSettings.Instances);
            // max concurrent consumers should reach number of desired instances
            time.ElapsedMilliseconds.Should().BeInRange(minPossibleTime, maxPossibleTime);

            var percent = Math.Round(100f * (time.ElapsedMilliseconds - minPossibleTime) / (maxPossibleTime - minPossibleTime), 2);
            Log.InfoFormat(CultureInfo.InvariantCulture, "The execution time was {0}% away from the best possible time", percent); // smallest number is better
        }

        [Fact]
        public void WhenRequestExpiredThenOnMessageExpiredIsCalled()
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

            var p = new ConsumerInstancePool<SomeRequest>(consumerSettings, _busMock.Bus, x => Array.Empty<byte>());

            var request = new SomeRequest();
            string requestId;
            string replyTo;
            DateTimeOffset? expires = _busMock.CurrentTime.AddSeconds(-10);
            _busMock.BusMock.Setup(x => x.DeserializeRequest(typeof(SomeRequest), It.IsAny<byte[]>(), out requestId, out replyTo, out expires))
                .Returns(request);

            // act
            p.ProcessMessage(request).Wait();

            // assert
            _busMock.HandlerMock.Verify(x => x.OnHandle(It.IsAny<SomeRequest>(), It.IsAny<string>()),
                Times.Never); // the handler should not be called

            onMessageExpiredMock.Verify(x => x(consumerSettings, request), Times.Once); // callback called once
        }

        [Fact]
        public void WhenRequestFailsThenOnMessageFaultIsCalledAndErrorResponseIsSent()
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

            var p = new ConsumerInstancePool<SomeRequest>(consumerSettings, _busMock.Bus, x => Array.Empty<byte>());

            var request = new SomeRequest();
            string requestId;
            string replyTo = "reply-topic";
            DateTimeOffset? expires;
            _busMock.BusMock.Setup(x =>
                    x.DeserializeRequest(typeof(SomeRequest), It.IsAny<byte[]>(), out requestId, out replyTo,
                        out expires))
                .Returns(request);

            var ex = new Exception("Something went bad");
            _busMock.HandlerMock.Setup(x => x.OnHandle(request, consumerSettings.Topic))
                .Returns(Task.FromException<SomeResponse>(ex));

            // act
            p.ProcessMessage(request).Wait();

            // assert
            _busMock.HandlerMock.Verify(x => x.OnHandle(request, consumerSettings.Topic),
                Times.Once); // handler called once

            onMessageFaultMock.Verify(x => x(consumerSettings, request, ex), Times.Once); // callback called once
            _busMock.BusMock.Verify(x =>
                x.PublishToTransport(typeof(SomeResponse), It.IsAny<SomeResponse>(), replyTo, It.IsAny<byte[]>()));
        }

        [Fact]
        public void WhenMessageFailsThenOnMessageFaultIsCalled()
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

            var p = new ConsumerInstancePool<SomeMessage>(consumerSettings, _busMock.Bus, x => Array.Empty<byte>());

            var message = new SomeMessage();
            _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeMessage), It.IsAny<byte[]>())).Returns(message);

            var ex = new Exception("Something went bad");
            _busMock.ConsumerMock.Setup(x => x.OnHandle(message, consumerSettings.Topic))
                .Returns(Task.FromException<SomeResponse>(ex));

            // act
            p.ProcessMessage(message).Wait();

            // assert
            _busMock.ConsumerMock.Verify(x => x.OnHandle(message, consumerSettings.Topic),
                Times.Once); // handler called once

            onMessageFaultMock.Verify(x => x(consumerSettings, message, ex), Times.Once); // callback called once
        }
    }
}