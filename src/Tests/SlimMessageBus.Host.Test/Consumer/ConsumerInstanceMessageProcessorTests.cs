namespace SlimMessageBus.Host.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Moq;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.DependencyResolver;
    using SlimMessageBus.Host.Interceptor;
    using Xunit;

    public class ConsumerInstanceMessageProcessorTests
    {
        private readonly MessageBusMock _busMock;

        public ConsumerInstanceMessageProcessorTests()
        {
            _busMock = new MessageBusMock();
        }

        private static MessageWithHeaders EmptyMessageWithHeadersProvider<T>(T msg) => new(Array.Empty<byte>());


        [Fact]
        public async Task When_RequestExpired_Then_OnMessageExpiredIsCalled()
        {
            // arrange
            var onMessageExpiredMock = new Mock<Action<IMessageBus, AbstractConsumerSettings, object, object>>();

            var consumerSettings = new HandlerBuilder<SomeRequest, SomeResponse>(new MessageBusSettings()).Topic(null).WithHandler<IRequestHandler<SomeRequest, SomeResponse>>().ConsumerSettings;
            consumerSettings.OnMessageExpired = onMessageExpiredMock.Object;

            MessageWithHeaders MessageProvider(SomeRequest request)
            {
                var m = EmptyMessageWithHeadersProvider(request);
                m.Headers.SetHeader(ReqRespMessageHeaders.Expires, _busMock.CurrentTime.AddSeconds(-10));
                m.Headers.SetHeader(ReqRespMessageHeaders.RequestId, "request-id");
                return m;
            }

            var p = new ConsumerInstanceMessageProcessor<SomeRequest>(consumerSettings, _busMock.Bus, MessageProvider);

            var request = new SomeRequest();
            _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeRequest), It.IsAny<byte[]>())).Returns(request);

            // act
            await p.ProcessMessage(request, null);

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

            var replyTo = "reply-topic";
            var requestId = "request-id";

            MessageWithHeaders MessageProvider(SomeRequest request)
            {
                var m = EmptyMessageWithHeadersProvider(request);
                m.Headers.SetHeader(ReqRespMessageHeaders.RequestId, requestId);
                m.Headers.SetHeader(ReqRespMessageHeaders.ReplyTo, replyTo);
                return m;
            }

            var p = new ConsumerInstanceMessageProcessor<SomeRequest>(consumerSettings, _busMock.Bus, MessageProvider);

            var request = new SomeRequest();
            _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeRequest), It.IsAny<byte[]>())).Returns(request);

            var ex = new Exception("Something went bad");
            _busMock.HandlerMock.Setup(x => x.OnHandle(request, consumerSettings.Path)).Returns(Task.FromException<SomeResponse>(ex));

            // act
            var exception = await p.ProcessMessage(request, null);

            // assert
            _busMock.HandlerMock.Verify(x => x.OnHandle(request, consumerSettings.Path), Times.Once); // handler called once

            onMessageFaultMock.Verify(
                x => x(_busMock.Bus, consumerSettings, request, ex, It.IsAny<object>()), Times.Once); // callback called once

            _busMock.BusMock.Verify(
                x => x.ProduceResponse(request, It.IsAny<IDictionary<string, object>>(), It.IsAny<SomeResponse>(), It.Is<IDictionary<string, object>>(m => (string)m[ReqRespMessageHeaders.RequestId] == requestId), It.IsAny<ConsumerSettings>()));

            exception.Should().BeSameAs(ex);
        }

        [Fact]
        public async Task When_MessageFails_Then_OnMessageFaultIsCalledAndExceptionReturned()
        {
            // arrange
            var onMessageFaultMock = new Mock<Action<IMessageBus, AbstractConsumerSettings, object, Exception, object>>();
            var consumerSettings = new ConsumerBuilder<SomeMessage>(new MessageBusSettings()).Topic("topic1").WithConsumer<IConsumer<SomeMessage>>().Instances(1).ConsumerSettings;
            consumerSettings.OnMessageFault = onMessageFaultMock.Object;

            var p = new ConsumerInstanceMessageProcessor<SomeMessage>(consumerSettings, _busMock.Bus, EmptyMessageWithHeadersProvider);

            var message = new SomeMessage();
            _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeMessage), It.IsAny<byte[]>())).Returns(message);

            var ex = new Exception("Something went bad");
            _busMock.ConsumerMock.Setup(x => x.OnHandle(message, consumerSettings.Path)).Returns(Task.FromException<SomeResponse>(ex));

            // act
            var exception = await p.ProcessMessage(message, null);

            // assert
            _busMock.ConsumerMock.Verify(x => x.OnHandle(message, consumerSettings.Path), Times.Once); // handler called once

            onMessageFaultMock.Verify(x => x(_busMock.Bus, consumerSettings, message, ex, It.IsAny<object>()), Times.Once); // callback called once           

            exception.Should().BeSameAs(exception);
        }

        [Fact]
        public async Task When_MessageArrives_Then_OnMessageArrivedIsCalled()
        {
            // arrange
            var message = new SomeMessage();
            var topic = "topic1";

            var onMessageArrivedMock = new Mock<Action<IMessageBus, AbstractConsumerSettings, object, string, object>>();

            _busMock.Bus.Settings.OnMessageArrived = onMessageArrivedMock.Object;

            var consumerSettings = new ConsumerBuilder<SomeMessage>(_busMock.Bus.Settings).Topic(topic).WithConsumer<IConsumer<SomeMessage>>().ConsumerSettings;
            consumerSettings.OnMessageArrived = onMessageArrivedMock.Object;

            var p = new ConsumerInstanceMessageProcessor<SomeMessage>(consumerSettings, _busMock.Bus, EmptyMessageWithHeadersProvider);

            _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeMessage), It.IsAny<byte[]>())).Returns(message);

            _busMock.ConsumerMock.Setup(x => x.OnHandle(message, consumerSettings.Path)).Returns(Task.CompletedTask);

            // act
            await p.ProcessMessage(message, null);

            // assert
            _busMock.ConsumerMock.Verify(x => x.OnHandle(message, consumerSettings.Path), Times.Once); // handler called once

            onMessageArrivedMock.Verify(x => x(_busMock.Bus, consumerSettings, message, topic, It.IsAny<object>()), Times.Exactly(2)); // callback called once for consumer and bus level
        }

        [Fact]
        public async Task When_MessageArrives_Then_ConsumerInterceptorIsCalled()
        {
            // arrange
            var message = new SomeMessage();
            var topic = "topic1";

            var messageConsumerInterceptor = new Mock<IConsumerInterceptor<SomeMessage>>();
            messageConsumerInterceptor
                .Setup(x => x.OnHandle(message, It.IsAny<CancellationToken>(), It.IsAny<Func<Task>>(), _busMock.Bus, topic, It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<object>()))
                .Callback((SomeMessage message, CancellationToken token, Func<Task> next, IMessageBus bus, string path, IReadOnlyDictionary<string, object> headers, object consumer) => next());

            _busMock.DependencyResolverMock.Setup(x => x.Resolve(typeof(IEnumerable<IConsumerInterceptor<SomeMessage>>))).Returns(new[] { messageConsumerInterceptor.Object });

            var consumerSettings = new ConsumerBuilder<SomeMessage>(_busMock.Bus.Settings).Topic(topic).WithConsumer<IConsumer<SomeMessage>>().ConsumerSettings;

            var p = new ConsumerInstanceMessageProcessor<SomeMessage>(consumerSettings, _busMock.Bus, EmptyMessageWithHeadersProvider);

            _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeMessage), It.IsAny<byte[]>())).Returns(message);

            _busMock.ConsumerMock.Setup(x => x.OnHandle(message, consumerSettings.Path)).Returns(Task.CompletedTask);

            // act
            await p.ProcessMessage(message, null);

            // assert
            _busMock.ConsumerMock.Verify(x => x.OnHandle(message, consumerSettings.Path), Times.Once); // handler called once
            _busMock.ConsumerMock.VerifyNoOtherCalls();

            messageConsumerInterceptor.Verify(x => x.OnHandle(message, It.IsAny<CancellationToken>(), It.IsAny<Func<Task>>(), _busMock.Bus, topic, It.IsAny<IReadOnlyDictionary<string, object>>(), _busMock.ConsumerMock.Object), Times.Once);
            messageConsumerInterceptor.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task When_MessageArrives_And_MessageScopeEnabled_Then_ScopeIsCreated_InstanceIsRetrivedFromScope_ConsumeMethodExecuted()
        {
            // arrange
            var topic = "topic1";

            var consumerSettings = new ConsumerBuilder<SomeMessage>(_busMock.Bus.Settings).Topic(topic).WithConsumer<IConsumer<SomeMessage>>().Instances(1).PerMessageScopeEnabled(true).ConsumerSettings;
            _busMock.BusMock.Setup(x => x.IsMessageScopeEnabled(consumerSettings)).Returns(true);

            var p = new ConsumerInstanceMessageProcessor<SomeMessage>(consumerSettings, _busMock.Bus, EmptyMessageWithHeadersProvider);

            var message = new SomeMessage();
            _busMock.SerializerMock.Setup(x => x.Deserialize(typeof(SomeMessage), It.IsAny<byte[]>())).Returns(message);

            _busMock.ConsumerMock.Setup(x => x.OnHandle(message, consumerSettings.Path)).Returns(Task.CompletedTask);

            Mock<IChildDependencyResolver> childScopeMock = null;

            _busMock.OnChildDependencyResolverCreated = mock =>
            {
                childScopeMock = mock;
            };

            // act
            await p.ProcessMessage(message, null);

            // assert
            _busMock.ConsumerMock.Verify(x => x.OnHandle(message, consumerSettings.Path), Times.Once); // handler called once
            _busMock.DependencyResolverMock.Verify(x => x.CreateScope(), Times.Once);
            _busMock.ChildDependencyResolverMocks.Count.Should().Be(0); // it has been disposed
            childScopeMock.Should().NotBeNull();
            childScopeMock.Verify(x => x.Resolve(typeof(IConsumer<SomeMessage>)), Times.Once);
        }
    }
}