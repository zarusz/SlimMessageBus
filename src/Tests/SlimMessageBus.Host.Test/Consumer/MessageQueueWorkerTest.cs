namespace SlimMessageBus.Host.Test.Consumer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using SlimMessageBus.Host.Config;
    using Xunit;

    public class MessageQueueWorkerTest
    {
        private readonly Mock<ConsumerInstancePoolMessageProcessor<SomeMessage>> _consumerInstancePoolMock;
        private readonly Mock<ICheckpointTrigger> _checkpointTriggerMock;

        public MessageQueueWorkerTest()
        {
            var busMock = new MessageBusMock();
            _checkpointTriggerMock = new Mock<ICheckpointTrigger>();

            var consumerSettings = new ConsumerBuilder<SomeMessage>(new MessageBusSettings()).Topic(null).WithConsumer<IConsumer<SomeMessage>>().Instances(2).ConsumerSettings;

            MessageWithHeaders MessageProvider(SomeMessage m) => new MessageWithHeaders(Array.Empty<byte>());
            _consumerInstancePoolMock = new Mock<ConsumerInstancePoolMessageProcessor<SomeMessage>>(consumerSettings, busMock.BusMock.Object, (Func<SomeMessage, MessageWithHeaders>)MessageProvider, null);
        }

        [Fact]
        public void WhenCommitThenWaitsOnAllMessagesToComplete()
        {
            // arrange
            var w = new MessageQueueWorker<SomeMessage>(_consumerInstancePoolMock.Object, _checkpointTriggerMock.Object, NullLoggerFactory.Instance);

            var numFinishedMessages = 0;
            _consumerInstancePoolMock.Setup(x => x.ProcessMessage(It.IsAny<SomeMessage>(), It.IsAny<IMessageTypeConsumerInvokerSettings>())).Returns(() => Task.Delay(50).ContinueWith(t => { Interlocked.Increment(ref numFinishedMessages); return (Exception)null; }, TaskScheduler.Current));

            const int numMessages = 100;
            for (var i = 0; i < numMessages; i++)
            {
                w.Submit(new SomeMessage());
            }

            // act
            var result = w.WaitAll().Result;

            // assert
            result.Success.Should().BeTrue();
            numFinishedMessages.Should().Be(numMessages);
        }

        [Fact]
        public void GivenIfSomeMessageFailsWhenCommitThenReturnsFirstNonFailedMessage()
        {
            // arrange
            var w = new MessageQueueWorker<SomeMessage>(_consumerInstancePoolMock.Object, _checkpointTriggerMock.Object, NullLoggerFactory.Instance);

            var taskQueue = new Queue<Task<Exception>>();
            taskQueue.Enqueue(Task.FromResult<Exception>(null));
            taskQueue.Enqueue(Task.Delay(3000).ContinueWith(x => (Exception)null));
            taskQueue.Enqueue(Task.FromException<Exception>(new Exception()));
            taskQueue.Enqueue(Task.FromResult<Exception>(null));
            taskQueue.Enqueue(Task.FromException<Exception>(new Exception()));
            taskQueue.Enqueue(Task.FromResult<Exception>(null));

            var messages = taskQueue.Select(x => new SomeMessage()).ToArray();

            _consumerInstancePoolMock.Setup(x => x.ProcessMessage(It.IsAny<SomeMessage>(), It.IsAny<IMessageTypeConsumerInvokerSettings>())).Returns(() => taskQueue.Dequeue());

            foreach (var t in messages)
            {
                w.Submit(t);
            }

            // act
            var result = w.WaitAll().Result;

            // assert
            result.Success.Should().BeFalse();
            result.LastSuccessMessage.Should().BeSameAs(messages[1]);
        }
    }
}
