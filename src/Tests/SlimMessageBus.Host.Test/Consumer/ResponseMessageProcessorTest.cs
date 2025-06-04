namespace SlimMessageBus.Host.Test.Consumer;

public class ResponseMessageProcessorTest
{
    private readonly RequestResponseSettings _settings;
    private readonly Mock<MessageProvider<object>> _messageProviderMock;
    private readonly Mock<IPendingRequestStore> _pendingRequestStoreMock;
    private readonly ResponseMessageProcessor<object> _subject;
    private readonly object _transportMessage;
    private readonly Dictionary<string, object> _messageHeaders;

    public ResponseMessageProcessorTest()
    {
        _settings = new RequestResponseSettings();
        _messageProviderMock = new Mock<MessageProvider<object>>();
        _pendingRequestStoreMock = new Mock<IPendingRequestStore>();
        _subject = new ResponseMessageProcessor<object>(NullLoggerFactory.Instance,
                                                        _settings,
                                                        _messageProviderMock.Object,
                                                        _pendingRequestStoreMock.Object,
                                                        TimeProvider.System);
        _transportMessage = new object();
        _messageHeaders = [];
    }

    [Fact]
    public async Task When_ProcessMessage_Given_NoRequestIdHeader_Then_ExceptionResult()
    {
        // arrange

        // act
        var r = await _subject.ProcessMessage(_transportMessage, _messageHeaders, null, null);

        // assert
        r.Exception.Should().BeOfType<ConsumerMessageBusException>();
        r.Response.Should().BeNull();
        r.Result.Should().Be(ProcessResult.Failure);
    }

    [Fact]
    public async Task When_ProcessMessage_Given_NonExistendRequestId_Then_ResponseIsNullAndNoError()
    {
        // arrange
        _messageHeaders[ReqRespMessageHeaders.RequestId] = "requestId";

        // act
        var r = await _subject.ProcessMessage(_transportMessage, _messageHeaders, null, null);

        // assert
        r.Exception.Should().BeNull();
        r.Response.Should().BeNull();
        r.Result.Should().Be(ProcessResult.Success);
    }

    [Fact]
    public async Task When_ProcessMessage_Given_ResponseIsFaulted_Then_ExceptionResult()
    {
        // arrange
        var requestId = "requestId";
        var responseError = "the error";
        _messageHeaders[ReqRespMessageHeaders.RequestId] = requestId;
        _messageHeaders[ReqRespMessageHeaders.Error] = responseError;

        var pendingRequestState = new PendingRequestState(requestId,
                                                          new object(),
                                                          typeof(object),
                                                          typeof(object),
                                                          DateTimeOffset.UtcNow,
                                                          DateTimeOffset.UtcNow.AddHours(2),
                                                          default);

        _pendingRequestStoreMock.Setup(x => x.GetById(requestId)).Returns(pendingRequestState);

        // act
        var r = await _subject.ProcessMessage(_transportMessage, _messageHeaders, null, null);

        // assert
        r.Exception.Should().BeNull();
        r.Response.Should().BeNull();
        r.Result.Should().Be(ProcessResult.Success);

        Func<Task> act = () => pendingRequestState.TaskCompletionSource.Task;
        await act.Should().ThrowAsync<RequestHandlerFaultedMessageBusException>().WithMessage(responseError);
    }

    [Fact]
    public async Task When_ProcessMessage_Given_ResponseArrivedOnTime_Then_TaskSourceIsResolved()
    {
        // arrange
        var requestId = "requestId";
        var response = new object();

        _messageProviderMock.Setup(x => x(response.GetType(), It.IsAny<IReadOnlyDictionary<string, object>>(), _transportMessage)).Returns(response);

        _messageHeaders[ReqRespMessageHeaders.RequestId] = requestId;

        var pendingRequestState = new PendingRequestState(requestId,
                                                          new object(),
                                                          typeof(object),
                                                          typeof(object),
                                                          DateTimeOffset.UtcNow,
                                                          DateTimeOffset.UtcNow.AddHours(2),
                                                          default);

        _pendingRequestStoreMock.Setup(x => x.GetById(requestId)).Returns(pendingRequestState);

        // act
        var r = await _subject.ProcessMessage(_transportMessage, _messageHeaders, null, null);

        // assert
        r.Exception.Should().BeNull();
        r.Response.Should().BeNull();
        r.Result.Should().Be(ProcessResult.Success);

        var responseReturned = await pendingRequestState.TaskCompletionSource.Task;
        responseReturned.Should().BeSameAs(response);
    }

    [Fact]
    public async Task When_ProcessMessage_Given_ResponseCannotBeDeserialized_Then_TaskSourceIsException()
    {
        // arrange
        var requestId = "requestId";
        var ex = new Exception("Boom!");

        _messageProviderMock.Setup(x => x(typeof(object), It.IsAny<IReadOnlyDictionary<string, object>>(), _transportMessage)).Throws(ex);

        _messageHeaders[ReqRespMessageHeaders.RequestId] = requestId;

        var pendingRequestState = new PendingRequestState(requestId,
                                                          new object(),
                                                          typeof(object),
                                                          typeof(object),
                                                          DateTimeOffset.UtcNow,
                                                          DateTimeOffset.UtcNow.AddHours(2),
                                                          default);

        _pendingRequestStoreMock.Setup(x => x.GetById(requestId)).Returns(pendingRequestState);

        // act
        var r = await _subject.ProcessMessage(_transportMessage, _messageHeaders, null, null);

        // assert
        r.Exception.Should().BeNull();
        r.Response.Should().BeNull();
        r.Result.Should().Be(ProcessResult.Success);

        Func<Task> act = () => pendingRequestState.TaskCompletionSource.Task;
        await act.Should().ThrowAsync<Exception>();
    }
}
