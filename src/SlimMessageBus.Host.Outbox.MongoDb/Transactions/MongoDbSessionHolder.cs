namespace SlimMessageBus.Host.Outbox.MongoDb.Transactions;

/// <summary>
/// Scoped mutable holder for the active MongoDB session.
/// Set by <see cref="MongoDbTransactionService"/> when a transaction begins;
/// read by the <see cref="System.Lazy{T}"/> factory registered for
/// <c>Lazy&lt;IClientSessionHandle?&gt;</c> so consumers can access the session
/// lazily inside their <c>OnHandle</c> method.
/// </summary>
internal sealed class MongoDbSessionHolder
{
    public IClientSessionHandle? Session { get; set; }
}
