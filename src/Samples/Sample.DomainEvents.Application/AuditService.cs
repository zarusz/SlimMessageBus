namespace Sample.DomainEvents.Application;

using Microsoft.Extensions.Logging;

public class AuditService(ILogger<AuditService> logger) : IAuditService
{
    public Task Append(Guid entityId, string msg)
    {
        // Output to some logging storage
        logger.LogInformation("Entity {EntityId} changed with {Message}", entityId, msg);
        return Task.CompletedTask;
    }
}

public interface IAuditService
{
    Task Append(Guid entityId, string msg);
}

