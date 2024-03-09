namespace SlimMessageBus.Host.Test;

using SlimMessageBus.Host.Interceptor;

public class PublishInterceptorPipelineTests
{
    private readonly MessageBusMock _busMock;

    public PublishInterceptorPipelineTests()
    {
        _busMock = new MessageBusMock();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_Next_Then_InterceptorIsCalledAndTargetIsCalled(bool producerInterceptorRegisterd, bool publishInterceptorRegisterd)
    {
        // arrange
        var message = new SomeMessage();
        var topic = "topic1";

        var producerInterceptorMock = new Mock<IProducerInterceptor<SomeMessage>>();
        producerInterceptorMock
            .Setup(x => x.OnHandle(message, It.IsAny<Func<Task<object>>>(), It.IsAny<IProducerContext>()))
            .Returns((SomeMessage message, Func<Task<object>> next, IProducerContext context) => next());

        var producerInterceptors = producerInterceptorRegisterd ? new[] { producerInterceptorMock.Object } : null;

        var publishInterceptorMock = new Mock<IPublishInterceptor<SomeMessage>>();
        publishInterceptorMock
            .Setup(x => x.OnHandle(message, It.IsAny<Func<Task>>(), It.IsAny<IProducerContext>()))
            .Returns((SomeMessage message, Func<Task> next, IProducerContext context) => next());

        var publishInterceptors = publishInterceptorRegisterd ? new[] { publishInterceptorMock.Object } : null;

        var producerSettings = new ProducerBuilder<SomeMessage>(new ProducerSettings())
            .DefaultTopic(topic)
            .Settings;

        var context = new PublishContext
        {
            Path = topic,
            Headers = new Dictionary<string, object>(),
        };

        _busMock.BusMock
            .Setup(x => x.PublishInternal(message, context.Path, context.Headers, context.CancellationToken, producerSettings, _busMock.ServiceProviderMock.Object))
            .Returns(() => Task.FromResult<object>(null));

        var subject = new PublishInterceptorPipeline(_busMock.Bus, message, producerSettings, _busMock.ServiceProviderMock.Object, context, producerInterceptors: producerInterceptors, publishInterceptors: publishInterceptors);

        // act
        var result = await subject.Next();

        // assert
        result.Should().BeNull();

        if (producerInterceptorRegisterd)
        {
            producerInterceptorMock.Verify(x => x.OnHandle(message, It.IsAny<Func<Task<object>>>(), It.IsAny<IProducerContext>()), Times.Once);
        }
        producerInterceptorMock.VerifyNoOtherCalls();

        if (publishInterceptorRegisterd)
        {
            publishInterceptorMock.Verify(x => x.OnHandle(message, It.IsAny<Func<Task>>(), It.IsAny<IProducerContext>()), Times.Once);
        }
        publishInterceptorMock.VerifyNoOtherCalls();

        _busMock.BusMock.Verify(x => x.PublishInternal(message, context.Path, context.Headers, context.CancellationToken, producerSettings, _busMock.ServiceProviderMock.Object), Times.Once);
    }
}