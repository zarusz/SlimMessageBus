namespace SlimMessageBus.Host.Outbox.DbContext.Test;

public abstract class BaseOutboxIntegrationTest<T>(ITestOutputHelper testOutputHelper) : BaseIntegrationTest<T>(testOutputHelper)
{
    protected async Task PerformDbOperation(Func<CustomerContext, IOutboxMigrationService, Task> action)
    {
        var scope = ServiceProvider!.CreateScope();
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<CustomerContext>();
            var outboxMigrationService = scope.ServiceProvider.GetRequiredService<IOutboxMigrationService>();
            await action(context, outboxMigrationService);
        }
        finally
        {
            await ((IAsyncDisposable)scope).DisposeAsync();
        }
    }
}
