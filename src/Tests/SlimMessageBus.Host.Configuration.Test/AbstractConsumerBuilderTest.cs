namespace SlimMessageBus.Host.Test.Config;

public class AbstractConsumerBuilderTest
{
    [Theory]
    [InlineData(typeof(SomeMessage), typeof(SomeMessageConsumer))]
    [InlineData(typeof(SomeRequest), typeof(SomeRequestMessageHandler))]
    [InlineData(typeof(SomeMessage), typeof(SomeMessageConsumerEx))]
    [InlineData(typeof(SomeRequest), typeof(SomeRequestHandlerEx))]
    [InlineData(typeof(SomeRequest), typeof(SomeRequestHandlerExWithResponse))]
    public void When_SetupConsumerOnHandleMethod_Given_TypesThatImplementConsumerInterfaces_Then_AssignsConsumerMethodInfo(Type messageType, Type consumerType)
    {
        // arrange
        var consumerSettings = new ConsumerSettings();
        var invoker = new MessageTypeConsumerInvokerSettings(consumerSettings, messageType, consumerType);

        // act
        AbstractConsumerBuilder.SetupConsumerOnHandleMethod(invoker);

        // assert
        invoker.ConsumerMethodInfo.Should().NotBeNull();
        invoker.ConsumerMethodInfo.Should().BeSameAs(consumerType.GetMethod(nameof(IConsumer<object>.OnHandle)));
    }

    [Fact]
    public void When_Constructor_Then_ThrowsArgumentNullException_Given_SettingsIsNull()
    {
        // act
        var act = () => new TestConsumerBuilder(null, typeof(SomeMessage));

        // asseet
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void When_Constructor_Then_InitializeAndConsumerSettingsIsPopulated()
    {
        // arrange
        var settings = new MessageBusSettings();
        var messageType = typeof(SomeMessage);

        // act
        var builder = new TestConsumerBuilder(settings, messageType, "test-path");

        // assert
        builder.Settings.Should().BeSameAs(settings);
        builder.ConsumerSettings.MessageType.Should().Be(messageType);
        builder.ConsumerSettings.Path.Should().Be("test-path");
        settings.Consumers.Should().Contain(builder.ConsumerSettings);
    }

    [Fact]
    public void When_Constructor_Then_Then_PostConfigurationActionsIsEmpty()
    {
        // act
        var builder = new TestConsumerBuilder(new MessageBusSettings(), typeof(SomeMessage));

        // assert
        builder.PostConfigurationActions.Should().BeEmpty();
    }

    [Fact]
    public void When_AssertInvokerUnique_Then_ShouldThrowException_Given_InvokerExists()
    {
        // arrange
        var builder = new TestConsumerBuilder(new MessageBusSettings(), typeof(SomeMessage));
        var invoker = new MessageTypeConsumerInvokerSettings(builder.ConsumerSettings, typeof(SomeMessage), typeof(SomeMessageConsumer));
        builder.ConsumerSettings.Invokers.Add(invoker);

        // act
        var act = () => builder.AssertInvokerUnique(typeof(SomeMessageConsumer), typeof(SomeMessage));

        // assert
        act.Should().Throw<ConfigurationMessageBusException>();
    }

    [Fact]
    public void When_AssertInvokerUnique_Then_ShouldNotThrowException_Given_InvokerDoesNotExist()
    {
        // arrange
        var builder = new TestConsumerBuilder(new MessageBusSettings(), typeof(SomeMessage));

        // act
        var act = () => builder.AssertInvokerUnique(typeof(SomeMessageConsumer), typeof(SomeMessage));

        // assert
        act.Should().NotThrow();
    }

    [Fact]
    public void When_Do_Then_InvokeActionAndReturnSameInstance()
    {
        // arrange
        var builder = new TestConsumerBuilder(new MessageBusSettings(), typeof(SomeMessage));
        var called = false;

        // act
        var result = builder.Do(b =>
        {
            called = true;
            b.Should().BeSameAs(builder);
        });

        // assert
        called.Should().BeTrue();
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void When_Do_Then_ThrowsArgumentNullException_Given_ActionIsNull()
    {
        // arrange
        var builder = new TestConsumerBuilder(new MessageBusSettings(), typeof(SomeMessage));

        // act
        var act = () => builder.Do(null);

        // assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void When_WhenUndeclaredMessageTypeArrives_Then_InvokeActionAndReturnSameInstance()
    {
        // arrange
        var builder = new TestConsumerBuilder(new MessageBusSettings(), typeof(SomeMessage));
        var called = false;

        // act
        var result = builder.WhenUndeclaredMessageTypeArrives(settings =>
        {
            called = true;
            settings.Should().BeSameAs(builder.ConsumerSettings.UndeclaredMessageType);
        });

        // assert
        called.Should().BeTrue();
        result.Should().BeSameAs(builder);
    }

    // Helper test builder
    private class TestConsumerBuilder(MessageBusSettings settings, Type messageType, string path = null)
        : AbstractConsumerBuilder<TestConsumerBuilder>(settings, messageType, path)
    {
    }

    private class SomeMessageConsumerEx : IConsumer<IConsumerContext<SomeMessage>>
    {
        public Task OnHandle(IConsumerContext<SomeMessage> message, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private class SomeRequestHandlerEx : IRequestHandler<IConsumerContext<SomeRequest>>
    {
        public Task OnHandle(IConsumerContext<SomeRequest> message, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private class SomeRequestHandlerExWithResponse : IRequestHandler<IConsumerContext<SomeRequest>, SomeResponse>
    {
        public Task<SomeResponse> OnHandle(IConsumerContext<SomeRequest> message, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
