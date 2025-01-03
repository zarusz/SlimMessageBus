namespace SlimMessageBus.Host.Test.DependencyResolver;

using SlimMessageBus.Host.Test;

public class ConsumerMethodPostProcessorTest
{
    private readonly ConsumerMethodPostProcessor _processor;

    public ConsumerMethodPostProcessorTest()
    {
        _processor = new ConsumerMethodPostProcessor();
    }

    [Fact]
    public void When_Run_Given_ConsumerInvokerWithConsumerMethodInfo_And_WithoutConsumerMethod_Then_ConsumerMethodIsGenerated()
    {
        // arrange
        var settings = new MessageBusSettings();
        var consumerSettings = new ConsumerSettings();

        var invokerWithoutConsumerMethod = new MessageTypeConsumerInvokerSettings(consumerSettings, typeof(SomeMessage), typeof(IConsumer<SomeMessage>))
        {
            ConsumerMethodInfo = typeof(IConsumer<SomeMessage>).GetMethod(nameof(IConsumer<SomeMessage>.OnHandle)),
        };
        var existingConsumerMethod = Mock.Of<ConsumerMethod>();
        var invokerWithConsumerMethod = new MessageTypeConsumerInvokerSettings(consumerSettings, typeof(SomeMessage), typeof(IConsumer<SomeMessage>))
        {
            ConsumerMethodInfo = typeof(IConsumer<SomeMessage>).GetMethod(nameof(IConsumer<SomeMessage>.OnHandle)),
            ConsumerMethod = existingConsumerMethod
        };

        consumerSettings.Invokers.Add(invokerWithoutConsumerMethod);
        consumerSettings.Invokers.Add(invokerWithConsumerMethod);

        settings.Consumers.Add(consumerSettings);

        var consumerMock = new Mock<IConsumer<SomeMessage>>();
        var message = new SomeMessage();
        var consumerContextMock = new Mock<IConsumerContext>();
        var cancellationToken = default(CancellationToken);

        // act
        _processor.Run(settings);

        // assert
        invokerWithoutConsumerMethod.ConsumerMethod.Should().NotBeNull();
        invokerWithoutConsumerMethod.ConsumerMethod(consumerMock.Object, message, consumerContextMock.Object, cancellationToken);
        consumerMock.Verify(x => x.OnHandle(message, cancellationToken), Times.Once);

        invokerWithConsumerMethod.ConsumerMethod.Should().NotBeNull();
        invokerWithConsumerMethod.ConsumerMethod.Should().Be(existingConsumerMethod);
    }
}
