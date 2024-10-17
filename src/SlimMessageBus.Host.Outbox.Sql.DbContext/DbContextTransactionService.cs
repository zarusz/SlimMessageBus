namespace SlimMessageBus.Host.Outbox.Sql.DbContext;

public class DbContextTransactionService<TDbContext>(TDbContext dbContext, ISqlSettings sqlSettings)
    : AbstractSqlTransactionService((SqlConnection)dbContext.Database.GetDbConnection())
    where TDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public TDbContext DbContext { get; } = dbContext;

    public override SqlTransaction CurrentTransaction => (SqlTransaction)DbContext.Database.CurrentTransaction?.GetDbTransaction();

    protected override Task OnBeginTransaction()
    {
        return DbContext.Database.BeginTransactionAsync(sqlSettings.TransactionIsolationLevel);
    }

    protected override Task OnCompleteTransaction(bool transactionFailed)
    {
        if (transactionFailed)
        {
            DbContext.Database.RollbackTransaction();
        }
        else
        {
            DbContext.Database.CommitTransaction();
        }
        return Task.CompletedTask;
    }
}