namespace SlimMessageBus.Host.Outbox.Sql.Test;

public class SqlTransactionServiceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompletedTransaction_CanBeFollowedByFreshTransaction(bool rollback)
    {
        await using var transaction = new TestTransactionService();
        await transaction.BeginTransaction();
        if (rollback)
        {
            await transaction.RollbackTransaction();
        }
        else
        {
            await transaction.CommitTransaction();
        }

        await transaction.BeginTransaction();
        await transaction.CommitTransaction();

        transaction.BeginCount.Should().Be(2);
        transaction.Completions.Should().Equal(rollback, false);
    }

    [Fact]
    public async Task NestedTransaction_CommitsOnlyAtOutermostLevel()
    {
        await using var transaction = new TestTransactionService();
        await transaction.BeginTransaction();
        await transaction.BeginTransaction();
        await transaction.CommitTransaction();
        transaction.Completions.Should().BeEmpty();
        await transaction.CommitTransaction();

        transaction.BeginCount.Should().Be(1);
        transaction.Completions.Should().Equal(false);
    }

    [Fact]
    public async Task NestedRollback_EndsTransaction_AndNextTransactionIsNotFailed()
    {
        await using var transaction = new TestTransactionService();
        await transaction.BeginTransaction();
        await transaction.BeginTransaction();
        await transaction.RollbackTransaction();
        await transaction.BeginTransaction();
        await transaction.CommitTransaction();

        transaction.BeginCount.Should().Be(2);
        transaction.Completions.Should().Equal(true, false);
    }

    private sealed class TestTransactionService() : AbstractSqlTransactionService(null)
    {
        public int BeginCount { get; private set; }
        public List<bool> Completions { get; } = [];
        public override SqlTransaction CurrentTransaction => null;

        protected override Task OnBeginTransaction()
        {
            BeginCount++;
            return Task.CompletedTask;
        }

        protected override Task OnCompleteTransaction(bool transactionFailed)
        {
            Completions.Add(transactionFailed);
            return Task.CompletedTask;
        }
    }
}
