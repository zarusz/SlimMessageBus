namespace SlimMessageBus.Host.Sql;

internal static class ServiceScopeExtensions
{
    public static async ValueTask DisposeAsyncScope(this IServiceScope scope)
    {
        if (scope is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            scope.Dispose();
        }
    }
}
