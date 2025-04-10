﻿namespace SlimMessageBus.Host.Test;

using SlimMessageBus.Host.Interceptor;

public class SendInterceptorPipelineTests
{
    private readonly MessageBusMock _busMock;

    public SendInterceptorPipelineTests()
    {
        _busMock = new MessageBusMock();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_Next_Then_InterceptorIsCalledAndTargetIsCalled(bool producerInterceptorRegistered, bool sendInterceptorRegistered)
    {
        // arrange
        var request = new SomeRequest();
        var response = new SomeResponse();
        var topic = "topic1";

        var producerInterceptorMock = new Mock<IProducerInterceptor<SomeRequest>>();
        producerInterceptorMock
            .Setup(x => x.OnHandle(request, It.IsAny<Func<Task<object>>>(), It.IsAny<IProducerContext>()))
            .Returns((SomeRequest message, Func<Task<object>> next, IProducerContext context) => next());

        var producerInterceptors = producerInterceptorRegistered ? new[] { producerInterceptorMock.Object } : null;

        var sendInterceptorMock = new Mock<ISendInterceptor<SomeRequest, SomeResponse>>();
        sendInterceptorMock
            .Setup(x => x.OnHandle(request, It.IsAny<Func<Task<SomeResponse>>>(), It.IsAny<IProducerContext>()))
            .Returns((SomeRequest message, Func<Task<SomeResponse>> next, IProducerContext context) => next());

        var sendInterceptors = sendInterceptorRegistered ? new[] { sendInterceptorMock.Object } : null;

        var producerSettings = new ProducerBuilder<SomeRequest>(new ProducerSettings())
            .DefaultTopic(topic)
            .Settings;

        var context = new SendContext
        {
            Path = topic,
            Headers = new Dictionary<string, object>(),
        };

        _busMock.BusMock
            .Setup(x => x.SendInternal<SomeResponse>(request, context.Path, request.GetType(), typeof(SomeResponse), producerSettings, context.Created, context.Expires, context.RequestId, context.Headers, _busMock.Bus.MessageBusTarget, context.CancellationToken))
            .Returns(() => Task.FromResult(response));

        var subject = new SendInterceptorPipeline<SomeResponse>(_busMock.Bus, request, producerSettings, _busMock.Bus.MessageBusTarget, context, producerInterceptors: producerInterceptors, sendInterceptors: sendInterceptors);

        // act
        var result = await subject.Next();

        // assert
        result.Should().BeSameAs(response);

        if (producerInterceptorRegistered)
        {
            producerInterceptorMock.Verify(x => x.OnHandle(request, It.IsAny<Func<Task<object>>>(), It.IsAny<IProducerContext>()), Times.Once);
        }
        producerInterceptorMock.VerifyNoOtherCalls();

        if (sendInterceptorRegistered)
        {
            sendInterceptorMock.Verify(x => x.OnHandle(request, It.IsAny<Func<Task<SomeResponse>>>(), It.IsAny<IProducerContext>()), Times.Once);
        }
        sendInterceptorMock.VerifyNoOtherCalls();

        _busMock.BusMock.Verify(x => x.SendInternal<SomeResponse>(request, context.Path, request.GetType(), typeof(SomeResponse), producerSettings, context.Created, context.Expires, context.RequestId, context.Headers, _busMock.Bus.MessageBusTarget, context.CancellationToken), Times.Once);
    }
}
