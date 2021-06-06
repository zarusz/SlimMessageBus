﻿namespace SlimMessageBus.Host.Test
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Moq;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.DependencyResolver;
    using Xunit;

    public class ConsumerInstancePoolTest
    {
        private readonly MessageBusMock _busMock;

        public ConsumerInstancePoolTest()
        {
            _busMock = new MessageBusMock();
        }

        [Fact]
        public void When_NewInstance_Then_DoesNotResolveConsumerInstances()
        {
            // arrange
            var consumerSettings = new ConsumerBuilder<SomeMessage>(new MessageBusSettings()).Topic(null).WithConsumer<IConsumer<SomeMessage>>().Instances(2).ConsumerSettings;

            // act
            var p = new ConsumerInstancePoolMessageProcessor<SomeMessage>(consumerSettings, _busMock.Bus, x => Array.Empty<byte>());

            // assert
            _busMock.DependencyResolverMock.Verify(x => x.Resolve(typeof(IConsumer<SomeMessage>)), Times.Never);
        }

        [Fact]
        public void When_NewInstance_Then_DoesNotResolveHandlerInstances()
        {
            // arrange
            var consumerSettings = new HandlerBuilder<SomeRequest, SomeResponse>(new MessageBusSettings()).Topic(null).WithHandler<IRequestHandler<SomeRequest, SomeResponse>>().Instances(2).ConsumerSettings;

            // act
            var p = new ConsumerInstancePoolMessageProcessor<SomeRequest>(consumerSettings, _busMock.Bus, x => Array.Empty<byte>());

            // assert
            _busMock.DependencyResolverMock.Verify(x => x.Resolve(typeof(IRequestHandler<SomeRequest, SomeResponse>)), Times.Never);
        }

        [Fact]
        public async Task When_NInstancesConfigured_Then_ExactlyNConsumerInstancesAreWorking()
        {
            const int consumerTime = 500;
            const int consumerInstances = 8;
            const int messageCount = 500;

            // arrange
            var consumerSettings = new ConsumerBuilder<SomeMessage>(new MessageBusSettings()).Topic(null).WithConsumer<IConsumer<SomeMessage>>().Instances(consumerInstances).ConsumerSettings;

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

            var p = new ConsumerInstancePoolMessageProcessor<SomeMessage>(consumerSettings, _busMock.Bus, x => Array.Empty<byte>());

            // act
            var time = Stopwatch.StartNew();
            var tasks = new Task[messageCount];
            for (var i = 0; i < messageCount; i++)
            {
                tasks[i] = p.ProcessMessage(new SomeMessage());
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

        [Fact]
        public async Task When_RequestExpired_Then_OnMessageExpiredIsCalled()
        {
            // arrange
            var onMessageExpiredMock = new Mock<Action<IMessageBus, AbstractConsumerSettings, object, object>>();
            var consumerSettings = new HandlerBuilder<SomeRequest, SomeResponse>(new MessageBusSettings()).Topic(null).WithHandler<IRequestHandler<SomeRequest, SomeResponse>>().Instances(1).ConsumerSettings;
            consumerSettings.OnMessageExpired = onMessageExpiredMock.Object;

            var p = new ConsumerInstancePoolMessageProcessor<SomeRequest>(consumerSettings, _busMock.Bus, x => Array.Empty<byte>());

            var request = new SomeRequest();
            var requestMessage = new MessageWithHeaders();
            requestMessage.SetHeader(ReqRespMessageHeaders.Expires, _busMock.CurrentTime.AddSeconds(-10));

            _busMock.BusMock.Setup(x => x.DeserializeRequest(typeof(SomeRequest), It.IsAny<byte[]>(), out requestMessage)).Returns(request);

            // act
            await p.ProcessMessage(request);

            // assert
            _busMock.HandlerMock.Verify(x => x.OnHandle(It.IsAny<SomeRequest>(), It.IsAny<string>()), Times.Never); // the handler should not be called

            onMessageExpiredMock.Verify(x => x(_busMock.Bus, consumerSettings, request, It.IsAny<object>()), Times.Once); // callback called once
        }

        [Fact]
        public async Task When_RequestFails_Then_OnMessageFaultIsCalledAndErrorResponseIsSent()
        {
            // arrange
            var onMessageFaultMock = new Mock<Action<IMessageBus, AbstractConsumerSettings, object, Exception, object>>();
            var consumerSettings = new HandlerBuilder<SomeRequest, SomeResponse>(new MessageBusSettings()).Topic(null).WithHandler<IRequestHandler<SomeRequest, SomeResponse>>().Instances(1).ConsumerSettings;
            consumerSettings.OnMessageFault = onMessageFaultMock.Object;

            var p = new ConsumerInstancePoolMessageProcessor<SomeRequest>(consumerSettings, _busMock.Bus, x => Array.Empty<byte>());

            var request = new SomeRequest();
            var requestMessage = new MessageWithHeaders();
            var replyTo = "reply-topic";
            var requestId = "request-id";
            requestMessage.SetHeader(ReqRespMessageHeaders.RequestId, requestId);
            requestMessage.SetHeader(ReqRespMessageHeaders.ReplyTo, replyTo);
            _busMock.BusMock.Setup(x => x.DeserializeRequest(typeof(SomeRequest), It.IsAny<byte[]>(), out requestMessage)).Returns(request);

            var ex = new Exception("Something went bad");
            _busMock.HandlerMock.Setup(x => x.OnHandle(request, consumerSettings.Path)).Returns(Task.FromException<SomeResponse>(ex));

            // act
            var exception = await p.ProcessMessage(request);

            // assert
            _busMock.HandlerMock.Verify(x => x.OnHandle(request, consumerSettings.Path), Times.Once); // handler called once

            onMessageFaultMock.Verify(
                x => x(_busMock.Bus, consumerSettings, request, ex, It.IsAny<object>()), Times.Once); // callback called once

            _busMock.BusMock.Verify(
                x => x.ProduceResponse(request, requestMessage, It.IsAny<SomeResponse>(), It.Is<MessageWithHeaders>(m => m.Headers[ReqRespMessageHeaders.RequestId] == requestId), It.IsAny<ConsumerSettings>()));

            exception.Should().BeSameAs(ex);
        }

        [Fact]
        public async Task When_MessageFails_Then_OnMessageFaultIsCalledAndExceptionReturned()
        {
            // arrange
            var onMessageFaultMock = new Mock<Action<IMessageBus, AbstractConsumerSettings, object, Exception, object>>();
            var consumerSettings = new ConsumerBuilder<SomeMessage>(new MessageBusSettings()).Topic("topic1").WithConsumer<IConsumer<SomeMessage>>().Instances(1).ConsumerSettings;
            consumerSettings.OnMessageFault = onMessageFaultMock.Object;

            var p = new ConsumerInstancePoolMessageProcessor<SomeMessage>(consumerSettings, _busMock.Bus, x => Array.Empty<byte>());

            var message = new SomeMessage();
            _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeMessage), It.IsAny<byte[]>())).Returns(message);

            var ex = new Exception("Something went bad");
            _busMock.ConsumerMock.Setup(x => x.OnHandle(message, consumerSettings.Path)).Returns(Task.FromException<SomeResponse>(ex));

            // act
            var exception = await p.ProcessMessage(message);

            // assert
            _busMock.ConsumerMock.Verify(x => x.OnHandle(message, consumerSettings.Path), Times.Once); // handler called once

            onMessageFaultMock.Verify(x => x(_busMock.Bus, consumerSettings, message, ex, It.IsAny<object>()), Times.Once); // callback called once           

            exception.Should().BeSameAs(exception);
        }

        [Fact]
        public async Task When_MessageArrives_Then_OnMessageArrivedIsCalled()
        {
            // arrange
            var onMessageArrivedMock = new Mock<Action<IMessageBus, AbstractConsumerSettings, object, string, object>>();

            var topic = "topic1";

            _busMock.Bus.Settings.OnMessageArrived = onMessageArrivedMock.Object;

            var consumerSettings = new ConsumerBuilder<SomeMessage>(_busMock.Bus.Settings).Topic(topic).WithConsumer<IConsumer<SomeMessage>>().Instances(1).ConsumerSettings;
            consumerSettings.OnMessageArrived = onMessageArrivedMock.Object;

            var p = new ConsumerInstancePoolMessageProcessor<SomeMessage>(consumerSettings, _busMock.Bus, x => Array.Empty<byte>());

            var message = new SomeMessage();
            _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeMessage), It.IsAny<byte[]>())).Returns(message);

            _busMock.ConsumerMock.Setup(x => x.OnHandle(message, consumerSettings.Path)).Returns(Task.CompletedTask);

            // act
            await p.ProcessMessage(message);

            // assert
            _busMock.ConsumerMock.Verify(x => x.OnHandle(message, consumerSettings.Path), Times.Once); // handler called once

            onMessageArrivedMock.Verify(x => x(_busMock.Bus, consumerSettings, message, topic, It.IsAny<object>()), Times.Exactly(2)); // callback called once for consumer and bus level
        }

        [Fact]
        public async Task When_MessageArrives_And_MessageScopeEnabled_Then_ScopeIsCreated_InstanceIsRetrivedFromScope_ConsumeMethodExecuted()
        {
            // arrange
            var topic = "topic1";

            var consumerSettings = new ConsumerBuilder<SomeMessage>(_busMock.Bus.Settings).Topic(topic).WithConsumer<IConsumer<SomeMessage>>().Instances(1).PerMessageScopeEnabled(true).ConsumerSettings;
            _busMock.BusMock.Setup(x => x.IsMessageScopeEnabled(consumerSettings)).Returns(true);

            var p = new ConsumerInstancePoolMessageProcessor<SomeMessage>(consumerSettings, _busMock.Bus, x => Array.Empty<byte>());

            var message = new SomeMessage();
            _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeMessage), It.IsAny<byte[]>())).Returns(message);

            _busMock.ConsumerMock.Setup(x => x.OnHandle(message, consumerSettings.Path)).Returns(Task.CompletedTask);

            Mock<IDependencyResolver> childScopeMock = null;

            _busMock.OnChildDependencyResolverCreated = mock =>
            {
                childScopeMock = mock;
            };

            // act
            await p.ProcessMessage(message);

            // assert
            _busMock.ConsumerMock.Verify(x => x.OnHandle(message, consumerSettings.Path), Times.Once); // handler called once
            _busMock.DependencyResolverMock.Verify(x => x.CreateScope(), Times.Once);
            _busMock.ChildDependencyResolverMocks.Count.Should().Be(0); // it has been disposed
            childScopeMock.Should().NotBeNull();
            childScopeMock.Verify(x => x.Resolve(typeof(IConsumer<SomeMessage>)), Times.Once);
        }
    }
}