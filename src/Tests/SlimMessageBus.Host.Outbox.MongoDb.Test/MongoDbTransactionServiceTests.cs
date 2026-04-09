namespace SlimMessageBus.Host.Outbox.MongoDb.Test;

public class MongoDbTransactionServiceTests
{
    private readonly Mock<IMongoClient> _clientMock = new();
    private readonly Mock<IClientSessionHandle> _sessionMock = new();
    private readonly MongoDbSessionHolder _sessionHolder = new();
    private readonly MongoDbTransactionService _sut;

    public MongoDbTransactionServiceTests()
    {
        _clientMock
            .Setup(x => x.StartSessionAsync(It.IsAny<ClientSessionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_sessionMock.Object);

        _sut = new MongoDbTransactionService(_clientMock.Object, _sessionHolder);
    }

    [Fact]
    public async Task When_BeginTransaction_Given_NoActiveSession_Then_StartsSessionAndTransaction()
    {
        await _sut.BeginTransaction();

        _clientMock.Verify(x => x.StartSessionAsync(It.IsAny<ClientSessionOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        _sessionMock.Verify(x => x.StartTransaction(It.IsAny<TransactionOptions>()), Times.Once);
        _sut.CurrentSession.Should().BeSameAs(_sessionMock.Object);
        _sessionHolder.Session.Should().BeSameAs(_sessionMock.Object);
    }

    [Fact]
    public async Task When_CommitTransaction_Given_ActiveTransaction_Then_CommitsSessionAndCleansUp()
    {
        _sessionMock
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.BeginTransaction();
        await _sut.CommitTransaction();

        _sessionMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _sessionHolder.Session.Should().BeNull();
        _sut.CurrentSession.Should().BeNull();
    }

    [Fact]
    public async Task When_RollbackTransaction_Given_ActiveTransaction_Then_AbortsSessionAndCleansUp()
    {
        _sessionMock
            .Setup(x => x.AbortTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.BeginTransaction();
        await _sut.RollbackTransaction();

        _sessionMock.Verify(x => x.AbortTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _sessionHolder.Session.Should().BeNull();
        _sut.CurrentSession.Should().BeNull();
    }
}
