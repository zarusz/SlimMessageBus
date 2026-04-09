namespace SlimMessageBus.Host.Outbox.MongoDb.Transactions;

/// <summary>
/// Manages a MongoDB client session and transaction (requires a MongoDB replica set).
/// </summary>
internal class MongoDbTransactionService(IMongoClient mongoClient, MongoDbSessionHolder sessionHolder)
    : AbstractMongoDbTransactionService
{
    private IClientSessionHandle? _session;

    public override IClientSessionHandle? CurrentSession => _session;

    protected override async Task OnBeginTransaction()
    {
        _session = await mongoClient.StartSessionAsync();
        _session.StartTransaction();
        sessionHolder.Session = _session;
    }

    protected override async Task OnCompleteTransaction(bool transactionFailed)
    {
        if (_session == null)
        {
            return;
        }

        try
        {
            if (transactionFailed)
            {
                await _session.AbortTransactionAsync();
            }
            else
            {
                await _session.CommitTransactionAsync();
            }
        }
        finally
        {
            sessionHolder.Session = null;
            _session.Dispose();
            _session = null;
        }
    }
}
