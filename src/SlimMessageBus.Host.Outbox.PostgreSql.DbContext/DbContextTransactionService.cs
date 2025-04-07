namespace SlimMessageBus.Host.Outbox.PostgreSql.DbContext;

using System.Threading;

public class DbContextTransactionService<TDbContext>(TDbContext dbContext, PostgreSqlSettings sqlSettings)
    : AbstractPostgreSqlTransactionService((NpgsqlConnection)dbContext.Database.GetDbConnection())
    where TDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    private IDbContextTransaction? _currentTransaction;

    public TDbContext DbContext { get; } = dbContext;

    public override NpgsqlTransaction? CurrentTransaction => _currentTransaction?.GetDbTransaction() as NpgsqlTransaction;

    protected override async Task OnBeginTransaction()
    {
        _currentTransaction = await DbContext.Database.BeginTransactionAsync(sqlSettings.TransactionIsolationLevel);
    }

    protected override async Task OnCompleteTransaction(bool transactionFailed)
    {
        if (_currentTransaction != null)
        {
            if (transactionFailed)
            {
                await _currentTransaction.RollbackAsync();
            }
            else
            {
                await _currentTransaction.CommitAsync();
            }

#if !NETSTANDARD2_0
            await _currentTransaction.DisposeAsync();
#else
            _currentTransaction.Dispose();
#endif

            _currentTransaction = null;
        }
    }
}