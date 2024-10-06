namespace SlimMessageBus.Host.Outbox;

public interface IHasId
{
    object Id { get; }
}

public interface IHasId<out TOutboxMessageKey>: IHasId
{
    new TOutboxMessageKey Id { get; }
}
