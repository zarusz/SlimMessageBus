namespace Sample.DomainEvents.Application;

using Microsoft.Extensions.Logging;

public class AuditService : IAuditService
{
    private readonly ILogger<AuditService> _logger;

    public AuditService(ILogger<AuditService> logger) => _logger = logger;

    public Task Append(Guid entityId, string msg)
    {
        // Output to some logging storage
        _logger.LogInformation("Entity {EntityId} changed with {Message}", entityId, msg);
        return Task.CompletedTask;
    }
}

public interface IAuditService
{
    Task Append(Guid entityId, string msg);
}

