namespace SlimMessageBus.Host.Memory.Test;

using System.Text;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

using SlimMessageBus.Host.Config;
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
            .WithSerializer(_messageSerializerMock.Object)
            .ExpectRequestResponses(x =>
            {
                x.ReplyToTopic("responses");
                x.DefaultTimeout(TimeSpan.FromHours(1));
            });

        _settings = _builder.Settings;

        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(It.Is<Type>(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)))).Returns((Type t) =>
        {
            return Enumerable.Empty<object>();
        });

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
        _settings.Serializer = null;
        _providerSettings.EnableMessageSerialization = serializationEnabled;

        // act
        Action act = () => { var _ = _subject.Value; };

        // assert          
        if (serializationEnabled)
        {
            act.Should().Throw<ConfigurationMessageBusException>();
        }
        else
        {
            act.Should().NotThrow<ConfigurationMessageBusException>();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_Publish_Given_MessageSerializationSetting_Then_DeliversMessageInstanceToRespectiveConsumers(bool enableMessageSerialization)
    {
        // arrange
        const string topicA = "topic-a";
        const string topicA2 = "topic-a-2";
        const string topicB = "topic-b";

        _builder.Produce<SomeMessageA>(x => x.DefaultTopic(topicA));
        _builder.Produce<SomeMessageB>(x => x.DefaultTopic(topicB));
        _builder.Consume<SomeMessageA>(x => x.Topic(topicA).WithConsumer<SomeMessageAConsumer>());
        _builder.Consume<SomeMessageA>(x => x.Topic(topicA2).WithConsumer<SomeMessageAConsumer2>());
        _builder.Consume<SomeMessageB>(x => x.Topic(topicB).WithConsumer<SomeMessageBConsumer>());

        var aConsumerMock = new Mock<SomeMessageAConsumer>();
        var aConsumer2Mock = new Mock<SomeMessageAConsumer2>();
        var bConsumerMock = new Mock<SomeMessageBConsumer>();
        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeMessageAConsumer))).Returns(aConsumerMock.Object);
        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeMessageAConsumer2))).Returns(aConsumer2Mock.Object);
        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeMessageBConsumer))).Returns(bConsumerMock.Object);

        _providerSettings.EnableMessageSerialization = enableMessageSerialization;

        var m = new SomeMessageA(Guid.NewGuid());

        // act
        await _subject.Value.Publish(m);

        // assert
        if (enableMessageSerialization)
        {
            aConsumerMock.Verify(x => x.OnHandle(It.Is<SomeMessageA>(a => a.Equals(m))), Times.Once);
        }
        else
        {
            aConsumerMock.Verify(x => x.OnHandle(m), Times.Once);
        }
        aConsumerMock.VerifyNoOtherCalls();

        aConsumer2Mock.Verify(x => x.OnHandle(It.IsAny<SomeMessageA>()), Times.Never);
        aConsumer2Mock.VerifyNoOtherCalls();

        bConsumerMock.Verify(x => x.OnHandle(It.IsAny<SomeMessageB>()), Times.Never);
        bConsumerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_Publish_Given_PerMessageScopeEnabled_Then_TheScopeIsCreatedAndConsumerObtainedFromScope()
    {
        // arrange
        var m = new SomeMessageA(Guid.NewGuid());

        var consumerMock = new Mock<SomeMessageAConsumer>();
        consumerMock.Setup(x => x.OnHandle(m)).Returns(() => Task.CompletedTask);

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
        await _subject.Value.Publish(m);

        // assert
        _serviceProviderMock.ScopeFactoryMock.Verify(x => x.CreateScope(), Times.Once);
        _serviceProviderMock.ScopeFactoryMock.VerifyNoOtherCalls();

        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(ILoggerFactory)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IServiceScopeFactory)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IProducerInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IPublishInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IMessageBusLifecycleInterceptor>)), Times.Between(0, 2, Moq.Range.Inclusive));
        _serviceProviderMock.ProviderMock.VerifyNoOtherCalls();

        scopeProviderMock.Should().NotBeNull();
        scopeMock.Should().NotBeNull();

        scopeMock.VerifyGet(x => x.ServiceProvider, Times.Once);
        scopeMock.Verify(x => x.Dispose(), Times.Once);
        scopeMock.VerifyNoOtherCalls();

        scopeProviderMock.Verify(x => x.GetService(typeof(SomeMessageAConsumer)), Times.Once);
        scopeProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IConsumerInterceptor<SomeMessageA>>)), Times.Once);       

        consumerMock.Verify(x => x.OnHandle(m), Times.Once);
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
        await _subject.Value.Publish(m);

        // assert
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(ILoggerFactory)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IServiceScopeFactory)), Times.Never);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(SomeMessageAConsumer)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IProducerInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IPublishInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IConsumerInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IMessageBusLifecycleInterceptor>)), Times.Between(0, 2, Moq.Range.Inclusive));
        _serviceProviderMock.ProviderMock.VerifyNoOtherCalls();

        consumerMock.Verify(x => x.OnHandle(m), Times.Once);
        consumerMock.Verify(x => x.Dispose(), Times.Once);
        consumerMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(new object[] { true })]
    [InlineData(new object[] { false })]
    public async Task When_Publish_Given_PerMessageScopeDisabledOrEnabled_And_OutterBusCreatedMesssageScope_Then_TheScopeIsNotCreated_And_ConsumerObtainedFromCurrentMessageScope(bool isMessageScopeEnabled)
    {
        // arrange
        var consumerMock = new Mock<SomeMessageAConsumer>();

        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeMessageAConsumer))).Returns(() => consumerMock.Object);

        var currentScopeDependencyResolverMock = new Mock<IServiceProvider>();
        currentScopeDependencyResolverMock.Setup(x => x.GetService(typeof(SomeMessageAConsumer))).Returns(() => consumerMock.Object);

        const string topic = "topic-a";

        _builder.Produce<SomeMessageA>(x => x.DefaultTopic(topic));
        _builder.Consume<SomeMessageA>(x => x.Topic(topic).WithConsumer<SomeMessageAConsumer>().DisposeConsumerEnabled(true));
        _builder.PerMessageScopeEnabled(isMessageScopeEnabled);

        _providerSettings.EnableMessageSerialization = false;

        var m = new SomeMessageA(Guid.NewGuid());

        // set current scope
        MessageScope.Current = currentScopeDependencyResolverMock.Object;

        // act
        await _subject.Value.Publish(m);

        // assert

        // current scope is not changed
        MessageScope.Current.Should().BeSameAs(currentScopeDependencyResolverMock.Object);

        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(ILoggerFactory)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IServiceScopeFactory)), Times.Never);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(SomeMessageAConsumer)), Times.Never);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IProducerInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IPublishInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IMessageBusLifecycleInterceptor>)), Times.Between(0, 2, Moq.Range.Inclusive));
        _serviceProviderMock.ProviderMock.VerifyNoOtherCalls();

        currentScopeDependencyResolverMock.Verify(x => x.GetService(typeof(IServiceScopeFactory)), Times.Never);
        currentScopeDependencyResolverMock.Verify(x => x.GetService(typeof(SomeMessageAConsumer)), Times.Once);
        currentScopeDependencyResolverMock.Verify(x => x.GetService(typeof(IEnumerable<IConsumerInterceptor<SomeMessageA>>)), Times.Once);
        currentScopeDependencyResolverMock.VerifyNoOtherCalls();

        consumerMock.Verify(x => x.OnHandle(m), Times.Once);
        consumerMock.Verify(x => x.Dispose(), Times.Once);
        consumerMock.VerifyNoOtherCalls();
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
        await _subject.Value.Publish(m);

        // assert

        // current scope is not changed
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(ILoggerFactory)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IServiceScopeFactory)), Times.Never);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(SomeMessageAConsumer)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(SomeMessageAConsumer2)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IProducerInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IPublishInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IConsumerInterceptor<SomeMessageA>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IMessageBusLifecycleInterceptor>)), Times.Between(0, 2, Moq.Range.Inclusive));
        _serviceProviderMock.ProviderMock.VerifyNoOtherCalls();

        consumer1Mock.Verify(x => x.OnHandle(m), Times.Once);
        consumer1Mock.VerifyNoOtherCalls();

        consumer2Mock.Verify(x => x.OnHandle(m), Times.Once);
        consumer2Mock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_Send_Given_AConsumersAndHandlerOnSameTopic_Then_BothAreInvoked_And_ConsumerIsFirst_And_HandlerResponseIsUsed_And_InterceptorsAreLookedUp()
    {
        const string topic = "topic-a";

        var m = new SomeRequest(Guid.NewGuid());

        var sequenceOfConsumption = new MockSequence();

        var consumer1Mock = new Mock<SomeRequestConsumer>(MockBehavior.Strict);
        consumer1Mock.InSequence(sequenceOfConsumption).Setup(x => x.OnHandle(m)).CallBase();

        var consumer2Mock = new Mock<SomeRequestHandler>(MockBehavior.Strict);
        consumer2Mock.InSequence(sequenceOfConsumption).Setup(x => x.OnHandle(m)).CallBase();

        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeRequestConsumer))).Returns(() => consumer1Mock.Object);
        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeRequestHandler))).Returns(() => consumer2Mock.Object);

        _builder.Produce<SomeRequest>(x => x.DefaultTopic(topic));
        _builder.Consume<SomeRequest>(x => x.Topic(topic).WithConsumer<SomeRequestConsumer>());
        _builder.Handle<SomeRequest, SomeResponse>(x => x.Topic(topic).WithHandler<SomeRequestHandler>());

        // act
        var response = await _subject.Value.Send(m);

        // assert
        response.Should().NotBeNull();
        response.Id.Should().Be(m.Id);

        // current scope is not changed
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(ILoggerFactory)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IServiceScopeFactory)), Times.Never);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(SomeRequestConsumer)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(SomeRequestHandler)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IProducerInterceptor<SomeRequest>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<ISendInterceptor<SomeRequest, SomeResponse>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IConsumerInterceptor<SomeRequest>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IRequestHandlerInterceptor<SomeRequest, SomeResponse>>)), Times.Once);
        _serviceProviderMock.ProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IMessageBusLifecycleInterceptor>)), Times.Between(0, 2, Moq.Range.Inclusive));
        _serviceProviderMock.ProviderMock.VerifyNoOtherCalls();

        consumer2Mock.Verify(x => x.OnHandle(m), Times.Once);
        consumer2Mock.VerifyNoOtherCalls();

        consumer1Mock.Verify(x => x.OnHandle(m), Times.Once);
        consumer1Mock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_Publish_Given_AConsumersThatThrowsException_Then_ExceptionIsBubblingToPublisher()
    {
        const string topic = "topic-a";

        var m = new SomeRequest(Guid.NewGuid());

        var consumerMock = new Mock<SomeRequestConsumer>();
        consumerMock.Setup(x => x.OnHandle(m)).ThrowsAsync(new ApplicationException("Bad Request"));

        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeRequestConsumer))).Returns(() => consumerMock.Object);

        _builder.Produce<SomeRequest>(x => x.DefaultTopic(topic));
        _builder.Consume<SomeRequest>(x => x.Topic(topic).WithConsumer<SomeRequestConsumer>());

        // act
        var act = () => _subject.Value.Publish(m);

        // assert
        await act.Should().ThrowAsync<ApplicationException>();
    }

    [Fact]
    public async Task When_Send_Given_AHandlerThatThrowsException_Then_ExceptionIsBubblingToSender()
    {
        const string topic = "topic-a";

        var m = new SomeRequest(Guid.NewGuid());

        var consumerMock = new Mock<SomeRequestHandler>();
        consumerMock.Setup(x => x.OnHandle(m)).ThrowsAsync(new ApplicationException("Bad Request"));

        _serviceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeRequestHandler))).Returns(() => consumerMock.Object);

        _builder.Produce<SomeRequest>(x => x.DefaultTopic(topic));
        _builder.Handle<SomeRequest, SomeResponse>(x => x.Topic(topic).WithHandler<SomeRequestHandler>());

        // act
        var act = () => _subject.Value.Send(m);

        // assert
        await act.Should().ThrowAsync<ApplicationException>();
    }
}

public record SomeMessageA(Guid Value);

public record SomeMessageB(Guid Value);

public class SomeMessageAConsumer : IConsumer<SomeMessageA>, IDisposable
{
    public virtual void Dispose()
    {
        // Needed to check disposing
    }

    public virtual Task OnHandle(SomeMessageA messageA) => Task.CompletedTask;
}

public class SomeMessageAConsumer2 : IConsumer<SomeMessageA>
{
    public virtual Task OnHandle(SomeMessageA messageA) => Task.CompletedTask;
}

public class SomeMessageBConsumer : IConsumer<SomeMessageB>
{
    public virtual Task OnHandle(SomeMessageB message) => Task.CompletedTask;
}

public record SomeRequest(Guid Id) : IRequestMessage<SomeResponse>;

public record SomeResponse(Guid Id);

public class SomeRequestHandler : IRequestHandler<SomeRequest, SomeResponse>
{
    public virtual Task<SomeResponse> OnHandle(SomeRequest request) => Task.FromResult(new SomeResponse(request.Id));
}

public class SomeRequestConsumer : IConsumer<SomeRequest>
{
    public virtual Task OnHandle(SomeRequest message) => Task.CompletedTask;
}