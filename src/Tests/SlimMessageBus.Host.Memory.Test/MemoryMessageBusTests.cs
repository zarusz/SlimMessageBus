namespace SlimMessageBus.Host.Memory.Test;

using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Newtonsoft.Json;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Consumer;
using SlimMessageBus.Host.Interceptor;
using SlimMessageBus.Host.Serialization;
using SlimMessageBus.Host.Test.Common;

public class MemoryMessageBusTests
{
    private readonly Lazy<MemoryMessageBus> _subject;
    private readonly MessageBusSettings _settings;
    private readonly MessageBusBuilder _builder;
    private readonly MemoryMessageBusSettings _providerSettings = new();
    private readonly ServiceProviderMock _serviceProviderMock = new();
    private readonly Mock<IMessageSerializer> _messageSerializerMock = new();

    public MemoryMessageBusTests()
    {
        _builder = MessageBusBuilder.Create()
            .WithDependencyResolver(_serviceProviderMock.ProviderMock.Object)
            .ExpectRequestResponses(x =>
            {
                x.ReplyToTopic("responses");
                x.DefaultTimeout(TimeSpan.FromHours(1));
            });

        _settings = _builder.Settings;

        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(IMessageSerializer))).Returns(_messageSerializerMock.Object);
        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(IMessageTypeResolver))).Returns(new AssemblyQualifiedNameMessageTypeResolver());
        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(ICurrentTimeProvider))).Returns(new CurrentTimeProvider());
        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(RuntimeTypeCache))).Returns(new RuntimeTypeCache());
        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(It.Is<Type>(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)))).Returns((Type t) => Enumerable.Empty<object>());
        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(IEnumerable<IMessageBusLifecycleInterceptor>))).Returns(Array.Empty<IMessageBusLifecycleInterceptor>());
        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(IPendingRequestManager))).Returns(() => new PendingRequestManager(new InMemoryPendingRequestStore(), new CurrentTimeProvider(), NullLoggerFactory.Instance));

        _messageSerializerMock
            .Setup(x => x.Serialize(It.IsAny<Type>(), It.IsAny<object>()))
            .Returns((Type type, object message) => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)));
        _messageSerializerMock
            .Setup(x => x.Deserialize(It.IsAny<Type>(), It.IsAny<byte[]>()))
            .Returns((Type type, byte[] payload) => JsonConvert.DeserializeObject(Encoding.UTF8.GetString(payload), type));

        _subject = new Lazy<MemoryMessageBus>(() => new MemoryMessageBus(_settings, _providerSettings));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_Create_Given_MessageSerializationEnabled_And_NoSerializerProvided_Then_ThrowsExceptionOrNot(bool serializationEnabled)
    {
        // arrange
        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(IMessageSerializer))).Returns(null);

        _providerSettings.EnableMessageSerialization = serializationEnabled;

        // act
        Action act = () => { var _ = _subject.Value.Serializer; };

        // assert          
        if (serializationEnabled)
        {
            act.Should().Throw<ConfigurationMessageBusException>();
        }
        else
        {
            act.Should().NotThrow();
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task When_Publish_Given_MessageSerializationSetting_Then_DeliversMessageInstanceToRespectiveConsumers(bool enableMessageSerialization, bool enableMessageHeaders)
    {
        // arrange
        const string topicA = "topic-a";
        const string topicA2 = "topic-a-2";
        const string topicB = "topic-b";
        var headers = new Dictionary<string, object>
        {
            ["key1"] = "str",
            ["key2"] = 2
        };

        _builder.Produce<SomeMessageA>(x => x.DefaultTopic(topicA));
        _builder.Produce<SomeMessageB>(x => x.DefaultTopic(topicB));
        _builder.Consume<SomeMessageA>(x => x.Topic(topicA).WithConsumer<SomeMessageAConsumer>());
        _builder.Consume<SomeMessageA>(x => x.Topic(topicA2).WithConsumer<SomeMessageAConsumer2>());
        _builder.Consume<SomeMessageB>(x => x.Topic(topicB).WithConsumer<SomeMessageBConsumer>());

        var aConsumerMock = new Mock<SomeMessageAConsumer>() { CallBase = true };
        var aConsumer2Mock = new Mock<SomeMessageAConsumer2>();
        var bConsumerMock = new Mock<SomeMessageBConsumer>();

        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeMessageAConsumer))).Returns(aConsumerMock.Object);
        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeMessageAConsumer2))).Returns(aConsumer2Mock.Object);
        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeMessageBConsumer))).Returns(bConsumerMock.Object);

        _providerSettings.EnableMessageSerialization = enableMessageSerialization;
        _providerSettings.EnableMessageHeaders = enableMessageHeaders;

        var m = new SomeMessageA(Guid.NewGuid());

        // act
        await _subject.Value.ProducePublish(m, headers: headers);

        // assert
        if (enableMessageSerialization)
        {
            aConsumerMock.Verify(x => x.OnHandle(It.Is<SomeMessageA>(a => a.Equals(m)), It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            aConsumerMock.Verify(x => x.OnHandle(m, It.IsAny<CancellationToken>()), Times.Once);
        }

        aConsumerMock.VerifySet(x => x.Context = It.IsAny<IConsumerContext>(), Times.Once);
        aConsumerMock.VerifyNoOtherCalls();

        if (enableMessageHeaders)
        {
            // All passed headers should be present in the consumer headers
            headers.Should().BeSubsetOf(aConsumerMock.Object.Context.Headers);
        }
        else
        {
            // The headers should not be present in the consumer headers
            aConsumerMock.Object.Context.Headers.Should().BeNull();
        }

        aConsumer2Mock.Verify(x => x.OnHandle(It.IsAny<SomeMessageA>(), It.IsAny<CancellationToken>()), Times.Never);
        aConsumer2Mock.VerifyNoOtherCalls();

        bConsumerMock.Verify(x => x.OnHandle(It.IsAny<SomeMessageB>(), It.IsAny<CancellationToken>()), Times.Never);
        bConsumerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_Publish_Given_PerMessageScopeEnabled_Then_TheScopeIsCreatedAndConsumerObtainedFromScope()
    {
        // arrange
        var m = new SomeMessageA(Guid.NewGuid());

        var consumerMock = new Mock<SomeMessageAConsumer>();
        consumerMock.Setup(x => x.OnHandle(m, It.IsAny<CancellationToken>())).Returns(() => Task.CompletedTask);

        Mock<IServiceProvider> scopeProviderMock = null;
        Mock<IServiceScope> scopeMock = null;

        _serviceProviderMock.OnScopeCreated = (scopeProviderMockCreated, scopeMockCreated) =>
        {
            scopeProviderMockCreated.Setup(x => x.GetService(typeof(SomeMessageAConsumer))).Returns(() => consumerMock.Object);

            scopeProviderMock = scopeProviderMockCreated;
            scopeMock = scopeMockCreated;
        };

        const string topic = "topic-a";

        _builder.Produce<SomeMessageA>(x => x.DefaultTopic(topic));
        _builder.Consume<SomeMessageA>(x => x.Topic(topic).WithConsumer<SomeMessageAConsumer>());
        _builder.PerMessageScopeEnabled(true);

        _providerSettings.EnableMessageSerialization = false;

        // act
        await _subject.Value.ProducePublish(m);

        // assert
        _serviceProviderMock.ScopeFactoryMock.Verify(x => x.CreateScope(), Times.Once);
        _serviceProviderMock.ScopeFactoryMock.VerifyNoOtherCalls();

        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IProducerInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IPublishInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IMessageBusLifecycleInterceptor>)), Times.Between(0, 2, Moq.Range.Inclusive));

        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IServiceScopeFactory)), Times.Once);
        VerifyCommonServiceProvider();
        _serviceProviderMock.ProviderMock.VerifyNoOtherCalls();

        scopeProviderMock.Should().NotBeNull();
        scopeMock.Should().NotBeNull();

        scopeMock.VerifyGet(x => x.ServiceProvider, Times.Once);
        scopeMock.Verify(x => x.Dispose(), Times.Once);
        scopeMock.VerifyNoOtherCalls();

        scopeProviderMock.Verify(x => x.GetService(typeof(SomeMessageAConsumer)), Times.Once);
        scopeProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IConsumerInterceptor<SomeMessageA>>)), Times.Once);

        consumerMock.VerifySet(x => x.Context = It.IsAny<IConsumerContext>(), Times.Once);
        consumerMock.Verify(x => x.OnHandle(m, It.IsAny<CancellationToken>()), Times.Once);
        consumerMock.Verify(x => x.Dispose(), Times.Never);
        consumerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_Publish_Given_PerMessageScopeDisabled_Then_TheScopeIsNotCreatedAndConsumerObtainedFromRoot()
    {
        // arrange
        var consumerMock = new Mock<SomeMessageAConsumer>();

        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeMessageAConsumer))).Returns(() => consumerMock.Object);

        const string topic = "topic-a";

        _builder.Produce<SomeMessageA>(x => x.DefaultTopic(topic));
        _builder.Consume<SomeMessageA>(x => x.Topic(topic).WithConsumer<SomeMessageAConsumer>().DisposeConsumerEnabled(true));
        _builder.PerMessageScopeEnabled(false);

        _providerSettings.EnableMessageSerialization = false;

        var m = new SomeMessageA(Guid.NewGuid());

        // act
        await _subject.Value.ProducePublish(m);

        // assert
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IServiceProvider)), Times.Never);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(SomeMessageAConsumer)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IProducerInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IPublishInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IConsumerInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IMessageBusLifecycleInterceptor>)), Times.Between(0, 2, Moq.Range.Inclusive));
        VerifyCommonServiceProvider();
        _serviceProviderMock.ProviderMock.VerifyNoOtherCalls();

        consumerMock.Verify(x => x.OnHandle(m, It.IsAny<CancellationToken>()), Times.Once);
        consumerMock.VerifySet(x => x.Context = It.IsAny<IConsumerContext>(), Times.Once);
        consumerMock.Verify(x => x.Dispose(), Times.Once);
        consumerMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData([true])]
    [InlineData([false])]
    public async Task When_ProducePublish_Given_PerMessageScopeDisabledOrEnabled_And_OutterBusCreatedMesssageScope_Then_TheScopeIsNotCreated_And_ConsumerObtainedFromCurrentMessageScope(bool isMessageScopeEnabled)
    {
        // arrange
        var consumerMock = new Mock<SomeMessageAConsumer>();
        var currentServiceProviderMock = new ServiceProviderMock();
        var createdScopeServiceProviderMock = isMessageScopeEnabled ? currentServiceProviderMock.PrepareScopeServiceProvider() : null;

        if (isMessageScopeEnabled)
        {
            createdScopeServiceProviderMock.Setup(x => x.GetService(typeof(SomeMessageAConsumer))).Returns(() => consumerMock.Object);
        }
        else
        {
            currentServiceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeMessageAConsumer))).Returns(() => consumerMock.Object);
        }

        const string topic = "topic-a";

        _builder.Produce<SomeMessageA>(x => x.DefaultTopic(topic));
        _builder.Consume<SomeMessageA>(x => x.Topic(topic).WithConsumer<SomeMessageAConsumer>().DisposeConsumerEnabled(true));
        _builder.PerMessageScopeEnabled(isMessageScopeEnabled);

        _providerSettings.EnableMessageSerialization = false;

        var m = new SomeMessageA(Guid.NewGuid());

        // set current scope
        MessageScope.Current = null;

        // act
        await _subject.Value.ProducePublish(m, path: null, headers: null, new MessageBusProxy(_subject.Value, currentServiceProviderMock.ProviderMock.Object), default);

        // assert

        // current scope is not changed
        MessageScope.Current.Should().BeNull();

        consumerMock.VerifySet(x => x.Context = It.IsAny<IConsumerContext>(), Times.Once);
        consumerMock.Verify(x => x.OnHandle(m, It.IsAny<CancellationToken>()), Times.Once);
        consumerMock.Verify(x => x.Dispose(), Times.Once);
        consumerMock.VerifyNoOtherCalls();

        if (isMessageScopeEnabled)
        {
            currentServiceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IServiceScopeFactory)), Times.Once);

            createdScopeServiceProviderMock.Verify(x => x.GetService(typeof(SomeMessageAConsumer)), Times.Once);
            createdScopeServiceProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IConsumerInterceptor<SomeMessageA>>)), Times.Once);
        }
        else
        {
            currentServiceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IServiceScopeFactory)), Times.Never);

            currentServiceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(SomeMessageAConsumer)), Times.Once);
            currentServiceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IConsumerInterceptor<SomeMessageA>>)), Times.Once);
        }

        currentServiceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IProducerInterceptor<SomeMessageA>>)), Times.Once);
        currentServiceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IPublishInterceptor<SomeMessageA>>)), Times.Once);
        currentServiceProviderMock.ProviderMock.VerifyNoOtherCalls();

        VerifyCommonServiceProvider();

        _serviceProviderMock.ProviderMock.VerifyNoOtherCalls();
    }

    private void VerifyCommonServiceProvider()
    {
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(ILoggerFactory)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IMessageBusLifecycleInterceptor>)), Times.Between(0, 2, Moq.Range.Inclusive));
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IMessageTypeResolver)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(ICurrentTimeProvider)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(RuntimeTypeCache)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IPendingRequestManager)), Times.Once);
    }

    [Fact]
    public async Task When_Publish_Given_TwoConsumersOnSameTopic_Then_BothAreInvoked()
    {
        var consumer1Mock = new Mock<SomeMessageAConsumer>();
        var consumer2Mock = new Mock<SomeMessageAConsumer2>();

        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeMessageAConsumer))).Returns(() => consumer1Mock.Object);
        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeMessageAConsumer2))).Returns(() => consumer2Mock.Object);

        const string topic = "topic-a";

        _builder.Produce<SomeMessageA>(x => x.DefaultTopic(topic));
        _builder.Consume<SomeMessageA>(x => x.Topic(topic).WithConsumer<SomeMessageAConsumer>());
        _builder.Consume<SomeMessageA>(x => x.Topic(topic).WithConsumer<SomeMessageAConsumer2>());

        var m = new SomeMessageA(Guid.NewGuid());

        // act
        await _subject.Value.ProducePublish(m);

        // assert

        // current scope is not changed
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(ILoggerFactory)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IServiceScopeFactory)), Times.Never);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(SomeMessageAConsumer)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(SomeMessageAConsumer2)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IProducerInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IPublishInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IConsumerInterceptor<SomeMessageA>>)), Times.Once);

        VerifyCommonServiceProvider();

        _serviceProviderMock.ProviderMock.VerifyNoOtherCalls();

        consumer1Mock.VerifySet(x => x.Context = It.IsAny<IConsumerContext>(), Times.Once);
        consumer1Mock.Verify(x => x.OnHandle(m, It.IsAny<CancellationToken>()), Times.Once);
        consumer1Mock.VerifyNoOtherCalls();

        consumer2Mock.Verify(x => x.OnHandle(m, It.IsAny<CancellationToken>()), Times.Once);
        consumer2Mock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_Send_Given_AConsumersAndHandlerOnSameTopic_Then_BothAreInvoked_And_ConsumerIsFirst_And_HandlerResponseIsUsed_And_InterceptorsAreLookedUp()
    {
        const string topic = "topic-a";

        var m = new SomeRequest(Guid.NewGuid());

        var sequenceOfConsumption = new MockSequence();

        var consumer1Mock = new Mock<SomeRequestConsumer>(MockBehavior.Strict);
        consumer1Mock.InSequence(sequenceOfConsumption).Setup(x => x.OnHandle(m, It.IsAny<CancellationToken>())).CallBase();

        var consumer2Mock = new Mock<SomeRequestHandler>(MockBehavior.Strict);
        consumer2Mock.InSequence(sequenceOfConsumption).Setup(x => x.OnHandle(m, It.IsAny<CancellationToken>())).CallBase();

        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeRequestConsumer))).Returns(() => consumer1Mock.Object);
        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeRequestHandler))).Returns(() => consumer2Mock.Object);

        _builder.Produce<SomeRequest>(x => x.DefaultTopic(topic));
        _builder.Consume<SomeRequest>(x => x.Topic(topic).WithConsumer<SomeRequestConsumer>());
        _builder.Handle<SomeRequest, SomeResponse>(x => x.Topic(topic).WithHandler<SomeRequestHandler>());

        // act
        var response = await _subject.Value.ProduceSend<SomeResponse>(m);

        // assert
        response.Should().NotBeNull();
        response.Id.Should().Be(m.Id);

        // current scope is not changed
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IServiceScopeFactory)), Times.Never);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(SomeRequestConsumer)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(SomeRequestHandler)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IProducerInterceptor<SomeRequest>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<ISendInterceptor<SomeRequest, SomeResponse>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IConsumerInterceptor<SomeRequest>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IRequestHandlerInterceptor<SomeRequest, SomeResponse>>)), Times.Once);
        VerifyCommonServiceProvider();
        _serviceProviderMock.ProviderMock.VerifyNoOtherCalls();

        consumer2Mock.Verify(x => x.OnHandle(m, It.IsAny<CancellationToken>()), Times.Once);
        consumer2Mock.VerifyNoOtherCalls();

        consumer1Mock.Verify(x => x.OnHandle(m, It.IsAny<CancellationToken>()), Times.Once);
        consumer1Mock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task When_Publish_Given_AConsumersThatThrowsException_Then_ExceptionIsBubblingToPublisher(bool errorHandlerRegistered, bool errorHandlerHandlesError)
    {
        const string topic = "topic-a";

        var m = new SomeRequest(Guid.NewGuid());

        var consumerMock = new Mock<IConsumer<SomeRequest>>();
        consumerMock
            .Setup(x => x.OnHandle(m, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationException("Bad Request"));

        var consumerErrorHandlerMock = new Mock<IMemoryConsumerErrorHandler<SomeRequest>>();
        consumerErrorHandlerMock
            .Setup(x => x.OnHandleError(It.IsAny<SomeRequest>(), It.IsAny<IConsumerContext>(), It.IsAny<Exception>(), It.IsAny<int>()))
            .ReturnsAsync(() => errorHandlerHandlesError ? ProcessResult.Success : ProcessResult.Failure);

        _serviceProviderMock.ProviderMock
            .Setup(x => x.GetService(typeof(IConsumer<SomeRequest>)))
            .Returns(() => consumerMock.Object);

        if (errorHandlerRegistered)
        {
            _serviceProviderMock.ProviderMock
                .Setup(x => x.GetService(typeof(IMemoryConsumerErrorHandler<SomeRequest>)))
                .Returns(() => consumerErrorHandlerMock.Object);
        }

        _builder.Produce<SomeRequest>(x => x.DefaultTopic(topic));
        _builder.Consume<SomeRequest>(x => x.Topic(topic));

        // act
        var act = () => _subject.Value.ProducePublish(m);

        // assert
        if (errorHandlerRegistered && errorHandlerHandlesError)
        {
            await act.Should().NotThrowAsync();
        }
        else
        {
            await act.Should().ThrowAsync<ApplicationException>();
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task When_Send_Given_AHandlerThatThrowsException_Then_ExceptionIsBubblingToSender(bool errorHandlerRegistered, bool errorHandlerHandlesError)
    {
        const string topic = "topic-a";

        var m = new SomeRequest(Guid.NewGuid());

        var consumerMock = new Mock<IRequestHandler<SomeRequest, SomeResponse>>();
        consumerMock
            .Setup(x => x.OnHandle(m, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationException("Bad Request"));

        var consumerErrorHandlerMock = new Mock<IMemoryConsumerErrorHandler<SomeRequest>>();
        consumerErrorHandlerMock
            .Setup(x => x.OnHandleError(It.IsAny<SomeRequest>(), It.IsAny<IConsumerContext>(), It.IsAny<Exception>(), It.IsAny<int>()))
            .ReturnsAsync(() => errorHandlerHandlesError ? ProcessResult.SuccessWithResponse(null) : ProcessResult.Failure);

        _serviceProviderMock.ProviderMock
            .Setup(x => x.GetService(typeof(IRequestHandler<SomeRequest, SomeResponse>)))
            .Returns(() => consumerMock.Object);

        if (errorHandlerRegistered)
        {
            _serviceProviderMock.ProviderMock
                .Setup(x => x.GetService(typeof(IMemoryConsumerErrorHandler<SomeRequest>)))
                .Returns(() => consumerErrorHandlerMock.Object);
        }

        _builder.Produce<SomeRequest>(x => x.DefaultTopic(topic));
        _builder.Handle<SomeRequest, SomeResponse>(x => x.Topic(topic));

        // act
        var act = () => _subject.Value.ProduceSend<SomeResponse>(m);

        // assert
        if (errorHandlerRegistered && errorHandlerHandlesError)
        {
            await act.Should().NotThrowAsync();
        }
        else
        {
            await act.Should().ThrowAsync<ApplicationException>();
        }
    }

    [Fact]
    public async Task When_Publish_Given_NoConsumerRegistered_Then_NoOp()
    {
        const string topic = "topic-a";

        _builder.Produce<SomeRequest>(x => x.DefaultTopic(topic));

        var request = new SomeRequest(Guid.NewGuid());

        // act
        Func<Task> act = () => _subject.Value.ProducePublish(request);

        // assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task When_Send_Given_NoHandlerRegistered_Then_ResponseIsNull()
    {
        const string topic = "topic-a";

        _builder.Produce<SomeRequest>(x => x.DefaultTopic(topic));

        var request = new SomeRequest(Guid.NewGuid());

        // act
        var response = await _subject.Value.ProduceSend<SomeResponse>(request);

        // assert
        response.Should().BeNull();
    }
}

public record SomeMessageA(Guid Value);

public record SomeMessageB(Guid Value);

public class SomeMessageAConsumer : IConsumer<SomeMessageA>, IConsumerWithContext, IDisposable
{
    public virtual IConsumerContext Context { get; set; }

    public virtual void Dispose()
    {
        // Needed to check disposing
        GC.SuppressFinalize(this);
    }

    public virtual Task OnHandle(SomeMessageA messageA, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class GenericConsumer<T> : IConsumer<T>
{
    public Task OnHandle(T message, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class SomeMessageAConsumer2 : IConsumer<SomeMessageA>
{
    public virtual Task OnHandle(SomeMessageA messageA, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class SomeMessageBConsumer : IConsumer<SomeMessageB>
{
    public virtual Task OnHandle(SomeMessageB message, CancellationToken cancellationToken) => Task.CompletedTask;
}

public record SomeRequest(Guid Id) : IRequest<SomeResponse>;

public record SomeResponse(Guid Id);

public class SomeRequestHandler : IRequestHandler<SomeRequest, SomeResponse>
{
    public virtual Task<SomeResponse> OnHandle(SomeRequest request, CancellationToken cancellationToken) => Task.FromResult(new SomeResponse(request.Id));
}

public class SomeRequestConsumer : IConsumer<SomeRequest>
{
    public virtual Task OnHandle(SomeRequest message, CancellationToken cancellationToken) => Task.CompletedTask;
}

public record SomeRequestWithoutResponse(Guid Id) : IRequest;

public class SomeRequestWithoutResponseHandler : IRequestHandler<SomeRequestWithoutResponse>
{
    public virtual Task OnHandle(SomeRequestWithoutResponse request, CancellationToken cancellationToken) => Task.CompletedTask;
}
