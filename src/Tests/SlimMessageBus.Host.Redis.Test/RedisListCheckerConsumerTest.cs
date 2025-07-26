namespace SlimMessageBus.Host.Redis.Test;

using System.Text;

using Serialization;

using StackExchange.Redis;

public class RedisListCheckerConsumerTest
{
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<IMessageProcessor<MessageWithHeaders>> _messageProcessorMock;
    private readonly Mock<IMessageSerializer> _envelopeSerializerMock;
    private readonly RedisListCheckerConsumer _subject;
    private readonly string _queueName = "test-queue";
    private readonly MessageWithHeaders _testMessage;

    public RedisListCheckerConsumerTest()
    {
        _databaseMock = new Mock<IDatabase>();
        _messageProcessorMock = new Mock<IMessageProcessor<MessageWithHeaders>>();
        _envelopeSerializerMock = new Mock<IMessageSerializer>();
        var testPayload = "{\"Data\":\"test\"}"u8.ToArray();
        _testMessage = new MessageWithHeaders(testPayload, new Dictionary<string, object>());
        var queues = new[] { (_queueName, _messageProcessorMock.Object) };
        _subject = new RedisListCheckerConsumer(
            NullLogger<RedisListCheckerConsumer>.Instance,
            new List<IAbstractConsumerInterceptor>(),
            _databaseMock.Object,
            pollDelay: TimeSpan.FromMilliseconds(10),
            maxIdle: TimeSpan.FromMilliseconds(50),
            queues,
            _envelopeSerializerMock.Object);
    }

    [Fact]
    public async Task Should_ProcessMessage_AfterException()
    {
        // Arrange
        var processedMessages = new List<MessageWithHeaders>();
        var callCount = 0;

        _databaseMock
            .Setup(x => x.ListLeftPopAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new RedisConnectionException(ConnectionFailureType.SocketFailure, "Connection failed");
                if (callCount == 2)
                    return (RedisValue)"serialized-message";
                return RedisValue.Null;
            });

        _envelopeSerializerMock
            .Setup(x => x.Deserialize(typeof(MessageWithHeaders), null, It.IsAny<byte[]>(), null))
            .Returns(_testMessage);

        var tcs = new TaskCompletionSource();

        _messageProcessorMock
            .Setup(x => x.ProcessMessage(
                It.IsAny<MessageWithHeaders>(),
                It.IsAny<IReadOnlyDictionary<string, object>>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IServiceProvider>(),
                It.IsAny<CancellationToken>()))
            .Returns<MessageWithHeaders, IReadOnlyDictionary<string, object>, IDictionary<string, object>,
                IServiceProvider, CancellationToken>((msg, _, _, _, _) =>
            {
                processedMessages.Add(msg);
                tcs.SetResult();
                return Task.FromResult(new ProcessMessageResult { Result = ProcessResult.Success });
            });

        // Act
        _ = _subject.Start();
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1)); // Wait for processing or timeout
        await _subject.Stop();

        // Assert
        _testMessage.Should().BeEquivalentTo(processedMessages[0]);
    }

    [Fact]
    public async Task Should_ProcessMultipleMessages_Successfully()
    {
        // Arrange
        var processedMessages = new List<MessageWithHeaders>();
        var callCount = 0;
        var totalMessages = 3;
        var tcs = new TaskCompletionSource();

        _databaseMock
            .Setup(x => x.ListLeftPopAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount <= totalMessages) return (RedisValue)$"serialized-message-{callCount}";
                return RedisValue.Null;
            });

        _envelopeSerializerMock
            .Setup(x => x.Deserialize(typeof(MessageWithHeaders), null, It.IsAny<byte[]>(), null))
            .Returns((Type type, string _, byte[] bytes, string __) =>
            {
                // Simulate unique payload for each message
                return new MessageWithHeaders(bytes, new Dictionary<string, object>());
            });

        _messageProcessorMock
            .Setup(x => x.ProcessMessage(
                It.IsAny<MessageWithHeaders>(),
                It.IsAny<IReadOnlyDictionary<string, object>>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IServiceProvider>(),
                It.IsAny<CancellationToken>()))
            .Returns<MessageWithHeaders, IReadOnlyDictionary<string, object>, IDictionary<string, object>,
                IServiceProvider, CancellationToken>((msg, _, _, _, _) =>
            {
                processedMessages.Add(msg);
                if (processedMessages.Count == totalMessages) tcs.SetResult();

                return Task.FromResult(new ProcessMessageResult { Result = ProcessResult.Success });
            });

        // Act
        _ = _subject.Start();
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1)); // Wait for all messages or timeout
        await _subject.Stop();

        // Assert
        processedMessages.Count().Should().Be(totalMessages);
        for (var i = 0; i < totalMessages; i++)
        {
            var expectedPayload = Encoding.UTF8.GetBytes($"serialized-message-{i + 1}");
            processedMessages[i].Payload.Should().BeEquivalentTo(expectedPayload);
        }
    }
}