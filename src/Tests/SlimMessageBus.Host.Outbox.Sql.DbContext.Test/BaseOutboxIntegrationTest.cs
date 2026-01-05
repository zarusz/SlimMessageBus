namespace SlimMessageBus.Host.Outbox.Sql.DbContext.Test;

using System.Diagnostics;
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

    /// <summary>
    /// Wait for the outbox to be drained (no pending messages).
    /// </summary>
    /// <param name="timeoutSeconds">Maximum time to wait in seconds</param>
    protected async Task WaitForOutboxToBeDrained(int timeoutSeconds = 10)
    {
        var stopwatch = Stopwatch.StartNew();
        var lastPendingCount = int.MaxValue;
        var stuckCount = 0;
        const int maxStuckIterations = 5; // If count doesn't change after 5 checks, exit early
        
        while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
        {
            var scope = ServiceProvider!.CreateScope();
            try
            {
                var context = scope.ServiceProvider.GetRequiredService<CustomerContext>();
                
                // Query the Outbox table to count pending messages
                // DeliveryComplete = 0 means the message hasn't been delivered yet
                var connection = context.Database.GetDbConnection();
                var wasOpen = connection.State == System.Data.ConnectionState.Open;
                if (!wasOpen)
                {
                    await connection.OpenAsync();
                }

                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = $"SELECT COUNT(*) FROM [{CustomerContext.Schema}].[Outbox] WHERE [DeliveryComplete] = 0 AND [DeliveryAborted] = 0";
                    var result = await command.ExecuteScalarAsync();
                    var pendingCount = Convert.ToInt32(result);
                    
                    if (pendingCount == 0)
                    {
                        Logger.LogInformation("Outbox is drained (0 pending messages) after {Elapsed}", stopwatch.Elapsed);
                        return;
                    }
                    
                    // Track if count is stuck (not changing)
                    if (pendingCount == lastPendingCount)
                    {
                        stuckCount++;
                        if (stuckCount >= maxStuckIterations)
                        {
                            Logger.LogWarning("Outbox pending count stuck at {PendingCount} for {StuckIterations} iterations after {Elapsed}, exiting early", 
                                pendingCount, stuckCount, stopwatch.Elapsed);
                            return;
                        }
                    }
                    else
                    {
                        stuckCount = 0;
                        Logger.LogInformation("Outbox has {PendingCount} pending messages after {Elapsed}", pendingCount, stopwatch.Elapsed);
                        lastPendingCount = pendingCount;
                    }
                }
                finally
                {
                    if (!wasOpen)
                    {
                        await connection.CloseAsync();
                    }
                }
                
                await Task.Delay(250);
            }
            finally
            {
                await ((IAsyncDisposable)scope).DisposeAsync();
            }
        }
        
        Logger.LogWarning("Timeout waiting for outbox to drain after {Elapsed}", stopwatch.Elapsed);
    }
}
