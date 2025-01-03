namespace SlimMessageBus.Host.Outbox.Sql.DbContext.Test;

using SlimMessageBus.Host.Outbox.Sql.DbContext.Test.DataAccess;

public abstract class BaseOutboxIntegrationTest<T>(ITestOutputHelper output) : BaseIntegrationTest<T>(output)
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
