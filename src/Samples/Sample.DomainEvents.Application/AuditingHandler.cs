namespace Sample.DomainEvents.Application;

using Sample.DomainEvents.Domain;

using SlimMessageBus;

/// <summary>
/// The domain event handler for <see cref="OrderSubmittedEvent"/> that showcases how you can have multiple consumes for the same domain event.
/// Moreover the injected <see cref="IAuditService"/> is scoped (it pick up the HTTP request scope).
/// </summary>
public class AuditingHandler : IConsumer<OrderSubmittedEvent>
{
    private readonly IAuditService _auditService;

    public AuditingHandler(IAuditService auditService) => _auditService = auditService;

    public Task OnHandle(OrderSubmittedEvent e) => _auditService.Append(e.Order.Id, "The Order was submitted");
}
