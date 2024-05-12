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