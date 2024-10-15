namespace SlimMessageBus.Host.Outbox.DbContext;

public interface IHasDbContext
{
    Microsoft.EntityFrameworkCore.DbContext DbContext { get; }
}

