namespace SlimMessageBus.Host.Outbox.MongoDb.Test;

public class AbstractMongoDbTransactionServiceTests
{
    private readonly TestTransactionService _sut = new();

    [Fact]
    public async Task When_BeginTransaction_Given_FirstCall_Then_InvokesOnBeginTransaction()
    {
        await _sut.BeginTransaction();

        _sut.BeginCount.Should().Be(1);
    }

    [Fact]
    public async Task When_BeginTransaction_Given_Nested_Then_OnlyCallsOnBeginTransactionOnce()
    {
        await _sut.BeginTransaction();
        await _sut.BeginTransaction();

        _sut.BeginCount.Should().Be(1);
    }

    [Fact]
    public async Task When_BeginTransaction_Given_AlreadyCompleted_Then_ThrowsMessageBusException()
    {
        await _sut.BeginTransaction();
        await _sut.CommitTransaction();

        var act = () => _sut.BeginTransaction();

        await act.Should().ThrowAsync<MessageBusException>();
    }

    [Fact]
    public async Task When_CommitTransaction_Given_SingleTransaction_Then_CompletesWithoutFailure()
    {
        await _sut.BeginTransaction();
        await _sut.CommitTransaction();

        _sut.CommitCount.Should().Be(1);
        _sut.RollbackCount.Should().Be(0);
    }

    [Fact]
    public async Task When_CommitTransaction_Given_NestedTransactions_Then_OnlyCompletesOnLastCommit()
    {
        await _sut.BeginTransaction();
        await _sut.BeginTransaction();

        await _sut.CommitTransaction(); // inner — should not complete yet
        _sut.CommitCount.Should().Be(0);

        await _sut.CommitTransaction(); // outer — should now complete
        _sut.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task When_CommitTransaction_Given_NotStarted_Then_ThrowsMessageBusException()
    {
        var act = () => _sut.CommitTransaction();

        await act.Should().ThrowAsync<MessageBusException>();
    }

    [Fact]
    public async Task When_RollbackTransaction_Given_ActiveTransaction_Then_CompletesWithFailure()
    {
        await _sut.BeginTransaction();
        await _sut.RollbackTransaction();

        _sut.RollbackCount.Should().Be(1);
        _sut.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task When_RollbackTransaction_Given_NestedTransactions_Then_CompletesImmediatelyWithFailure()
    {
        await _sut.BeginTransaction();
        await _sut.BeginTransaction();

        // rollback inside nested transaction should fail-fast
        await _sut.RollbackTransaction();

        _sut.RollbackCount.Should().Be(1);
        _sut.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task When_RollbackTransaction_Given_NotStarted_Then_ThrowsMessageBusException()
    {
        var act = () => _sut.RollbackTransaction();

        await act.Should().ThrowAsync<MessageBusException>();
    }

    [Fact]
    public async Task When_DisposeAsync_Given_TransactionNotCompleted_Then_RollsBack()
    {
        await _sut.BeginTransaction();

        await _sut.DisposeAsync();

        _sut.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task When_DisposeAsync_Given_TransactionAlreadyCompleted_Then_DoesNotRollBack()
    {
        await _sut.BeginTransaction();
        await _sut.CommitTransaction();

        await _sut.DisposeAsync();

        _sut.RollbackCount.Should().Be(0);
    }

    [Fact]
    public async Task When_DisposeAsync_Given_NoTransactionStarted_Then_DoesNotRollBack()
    {
        await _sut.DisposeAsync();

        _sut.RollbackCount.Should().Be(0);
    }

    private sealed class TestTransactionService : AbstractMongoDbTransactionService
    {
        public int BeginCount { get; private set; }
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }

        public override IClientSessionHandle? CurrentSession => null;

        protected override Task OnBeginTransaction()
        {
            BeginCount++;
            return Task.CompletedTask;
        }

        protected override Task OnCompleteTransaction(bool transactionFailed)
        {
            if (transactionFailed)
                RollbackCount++;
            else
                CommitCount++;
            return Task.CompletedTask;
        }
    }
}
