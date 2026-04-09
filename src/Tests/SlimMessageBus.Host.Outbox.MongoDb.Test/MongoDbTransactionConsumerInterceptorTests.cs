namespace SlimMessageBus.Host.Outbox.MongoDb.Test;

using Microsoft.Extensions.Logging;

using SlimMessageBus.Host.Outbox.MongoDb.Interceptors;

public class MongoDbTransactionConsumerInterceptorTests
{
    private readonly Mock<ILogger<IMongoDbTransactionConsumerInterceptor>> _loggerMock = new();
    private readonly Mock<IMongoDbTransactionService> _transactionServiceMock = new();
    private readonly Mock<IConsumerContext> _contextMock = new();
    private readonly MongoDbTransactionConsumerInterceptor<SampleMessage> _sut;

    public MongoDbTransactionConsumerInterceptorTests()
    {
        _sut = new MongoDbTransactionConsumerInterceptor<SampleMessage>(
            _loggerMock.Object,
            _transactionServiceMock.Object);
    }

    [Fact]
    public async Task When_OnHandle_Given_NextSucceeds_Then_BeginsThenCommitsTransaction()
    {
        // arrange
        var message = new SampleMessage();
        var expectedResult = new object();

        _transactionServiceMock.Setup(x => x.BeginTransaction()).Returns(Task.CompletedTask);
        _transactionServiceMock.Setup(x => x.CommitTransaction()).Returns(Task.CompletedTask);

        // act
        var result = await _sut.OnHandle(message, () => Task.FromResult(expectedResult), _contextMock.Object);

        // assert
        result.Should().BeSameAs(expectedResult);
        _transactionServiceMock.Verify(x => x.BeginTransaction(), Times.Once);
        _transactionServiceMock.Verify(x => x.CommitTransaction(), Times.Once);
        _transactionServiceMock.Verify(x => x.RollbackTransaction(), Times.Never);
    }

    [Fact]
    public async Task When_OnHandle_Given_NextThrows_Then_RollsBackAndRethrows()
    {
        // arrange
        var message = new SampleMessage();
        var exception = new InvalidOperationException("consumer failed");

        _transactionServiceMock.Setup(x => x.BeginTransaction()).Returns(Task.CompletedTask);
        _transactionServiceMock.Setup(x => x.RollbackTransaction()).Returns(Task.CompletedTask);

        // act
        var act = async () => await _sut.OnHandle(
            message,
            () => throw exception,
            _contextMock.Object);

        // assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("consumer failed");
        _transactionServiceMock.Verify(x => x.BeginTransaction(), Times.Once);
        _transactionServiceMock.Verify(x => x.RollbackTransaction(), Times.Once);
        _transactionServiceMock.Verify(x => x.CommitTransaction(), Times.Never);
    }

    public record SampleMessage;
}
