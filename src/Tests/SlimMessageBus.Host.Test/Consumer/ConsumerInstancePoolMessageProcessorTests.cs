namespace SlimMessageBus.Host.Test
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Moq;
    using SlimMessageBus.Host.Config;
    using Xunit;

    public class ConsumerInstancePoolMessageProcessorTests
    {
        private readonly MessageBusMock _busMock;

        public ConsumerInstancePoolMessageProcessorTests()
        {
            _busMock = new MessageBusMock();
        }

        [Fact]
        public void When_Consume_Given_NewInstance_Then_DoesNotResolveConsumerInstances()
        {
            // arrange
            var consumerSettings = new ConsumerBuilder<SomeMessage>(new MessageBusSettings()).Topic(null).WithConsumer<IConsumer<SomeMessage>>().Instances(2).ConsumerSettings;

            // act
            var p = new ConsumerInstancePoolMessageProcessor<SomeMessage>(consumerSettings, _busMock.Bus, EmptyMessageWithHeadersProvider);

            // assert
            _busMock.DependencyResolverMock.Verify(x => x.Resolve(typeof(IConsumer<SomeMessage>)), Times.Never);
        }

        [Fact]
        public void When_Consume_Given_Then_DoesNotResolveHandlerInstances()
        {
            // arrange
            var consumerSettings = new HandlerBuilder<SomeRequest, SomeResponse>(new MessageBusSettings()).Topic(null).WithHandler<IRequestHandler<SomeRequest, SomeResponse>>().Instances(2).ConsumerSettings;

            // act
            var p = new ConsumerInstancePoolMessageProcessor<SomeRequest>(consumerSettings, _busMock.Bus, EmptyMessageWithHeadersProvider);

            // assert
            _busMock.DependencyResolverMock.Verify(x => x.Resolve(typeof(IRequestHandler<SomeRequest, SomeResponse>)), Times.Never);
        }

        [Fact]
        public async Task When_Consume_Given_NInstancesConfigured_Then_ExactlyNConsumerInstancesAreWorking()
        {
            const int consumerTime = 500;
            const int consumerInstances = 8;
            const int messageCount = 500;

            // arrange
            _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeMessage), It.IsAny<byte[]>())).Returns(new SomeMessage());

            var consumerSettings = new ConsumerBuilder<SomeMessage>(new MessageBusSettings()).Topic("topic").WithConsumer<IConsumer<SomeMessage>>().Instances(consumerInstances).ConsumerSettings;

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

            var p = new ConsumerInstancePoolMessageProcessor<SomeMessage>(consumerSettings, _busMock.Bus, EmptyMessageWithHeadersProvider);

            // act
            var time = Stopwatch.StartNew();
            var tasks = new Task[messageCount];
            for (var i = 0; i < messageCount; i++)
            {
                tasks[i] = p.ProcessMessage(new SomeMessage(), null);
            }

            await Task.WhenAll(tasks);

            time.Stop();

            // assert
            var minPossibleTime = messageCount * consumerTime / consumerSettings.Instances;
            var maxPossibleTime = messageCount * consumerTime;

            processedMessageCount.Should().Be(messageCount);
            maxInstances.Should().Be(consumerSettings.Instances);
            // max concurrent consumers should reach number of desired instances
            time.ElapsedMilliseconds.Should().BeInRange(minPossibleTime, maxPossibleTime);

            var percent = Math.Round(100f * (time.ElapsedMilliseconds - minPossibleTime) / (maxPossibleTime - minPossibleTime), 2);
            Console.WriteLine("The execution time was {0}% away from the best possible time", percent); // smallest number is better
        }

        private static MessageWithHeaders EmptyMessageWithHeadersProvider<T>(T msg) => new(Array.Empty<byte>());
    }
}