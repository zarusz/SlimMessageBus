using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Test.Consumer
{
    [TestClass]
    public class MessageQueueWorkerTest
    {
        private Mock<ConsumerInstancePool<SomeMessage>> _consumerInstancePoolMock;
        private MessageBusMock _busMock;

        [TestInitialize]
        public void Init()
        {
            _busMock = new MessageBusMock();

            var consumerSettings = new ConsumerSettings
            {
                Instances = 2,
                ConsumerMode = ConsumerMode.Subscriber,
                ConsumerType = typeof(IConsumer<SomeMessage>),
                MessageType = typeof(SomeMessage)
            };

            Func<SomeMessage, byte[]> payloadProvider = m => new byte[0];
            _consumerInstancePoolMock = new Mock<ConsumerInstancePool<SomeMessage>>(consumerSettings, _busMock.BusMock.Object, payloadProvider);
        }

        [TestMethod]
        public void Commit_WaitsOnAllMessagesToComplete()
        {
            // arrange
            var w = new MessageQueueWorker<SomeMessage>(_consumerInstancePoolMock.Object);

            var numFinishedMessages = 0;
            _consumerInstancePoolMock.Setup(x => x.ProcessMessage(It.IsAny<SomeMessage>())).Returns(() => Task.Delay(50).ContinueWith(t => Interlocked.Increment(ref numFinishedMessages)));

            SomeMessage lastMsg = null;
            const int numMessages = 100;
            for (var i = 0; i < numMessages; i++)
            {
                lastMsg = new SomeMessage();
                w.Submit(lastMsg);
            }

            // act
            var commitedMsg = w.Commit(lastMsg);

            // assert
            commitedMsg.Should().BeSameAs(lastMsg);
            numFinishedMessages.ShouldBeEquivalentTo(numMessages);
        }

    }
}
