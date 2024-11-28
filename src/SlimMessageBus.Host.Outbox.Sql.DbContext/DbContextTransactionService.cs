namespace SlimMessageBus.Host.Outbox.Sql.DbContext;

public class DbContextTransactionService<TDbContext>(TDbContext dbContext, ISqlSettings sqlSettings)
    : AbstractSqlTransactionService((SqlConnection)dbContext.Database.GetDbConnection())
    where TDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    private IDbContextTransaction _currentTransaction;

    public TDbContext DbContext { get; } = dbContext;

    public override SqlTransaction CurrentTransaction => _currentTransaction?.GetDbTransaction() as SqlTransaction;

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
            _currentTransaction.Dispose();
            _currentTransaction = null;
        }
    }
}