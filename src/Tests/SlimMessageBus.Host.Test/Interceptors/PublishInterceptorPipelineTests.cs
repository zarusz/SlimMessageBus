namespace SlimMessageBus.Host.Test;

using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Interceptor;

public class PublishInterceptorPipelineTests
{
    private readonly MessageBusMock _busMock = new();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_Next_Then_InterceptorIsCalledAndTargetIsCalled(bool producerInterceptorRegistered, bool publishInterceptorRegistered)
    {
        // arrange
        var message = new SomeMessage();
        var topic = "topic1";

        var producerInterceptorMock = new Mock<IProducerInterceptor<SomeMessage>>();
        producerInterceptorMock
            .Setup(x => x.OnHandle(message, It.IsAny<Func<Task<object>>>(), It.IsAny<IProducerContext>()))
            .Returns((SomeMessage message, Func<Task<object>> next, IProducerContext context) => next());

        var producerInterceptors = producerInterceptorRegistered ? new[] { producerInterceptorMock.Object } : null;

        var publishInterceptorMock = new Mock<IPublishInterceptor<SomeMessage>>();
        publishInterceptorMock
            .Setup(x => x.OnHandle(message, It.IsAny<Func<Task>>(), It.IsAny<IProducerContext>()))
            .Returns((SomeMessage message, Func<Task> next, IProducerContext context) => next());

        var publishInterceptors = publishInterceptorRegistered ? new[] { publishInterceptorMock.Object } : null;

        var producerSettings = new ProducerBuilder<SomeMessage>(new ProducerSettings())
            .DefaultTopic(topic)
            .Settings;

        var context = new PublishContext
        {
            Path = topic,
            Headers = new Dictionary<string, object>(),
        };

        _busMock.BusMock
            .Setup(x => x.ProduceToTransport(message, producerSettings.MessageType, context.Path, context.Headers, _busMock.Bus.MessageBusTarget, context.CancellationToken))
            .Returns(() => Task.FromResult<object>(null));

        var subject = new PublishInterceptorPipeline(_busMock.Bus, new RuntimeTypeCache(), message, producerSettings, _busMock.Bus.MessageBusTarget, context, producerInterceptors: producerInterceptors, publishInterceptors: publishInterceptors);

        // act
        var result = await subject.Next();

        // assert
        result.Should().BeNull();

        if (producerInterceptorRegistered)
        {
            producerInterceptorMock.Verify(x => x.OnHandle(message, It.IsAny<Func<Task<object>>>(), It.IsAny<IProducerContext>()), Times.Once);
        }
        producerInterceptorMock.VerifyNoOtherCalls();

        if (publishInterceptorRegistered)
        {
            publishInterceptorMock.Verify(x => x.OnHandle(message, It.IsAny<Func<Task>>(), It.IsAny<IProducerContext>()), Times.Once);
        }
        publishInterceptorMock.VerifyNoOtherCalls();

        _busMock.BusMock.Verify(x => x.ProduceToTransport(message, producerSettings.MessageType, context.Path, context.Headers, _busMock.Bus.MessageBusTarget, context.CancellationToken), Times.Once);
    }
}