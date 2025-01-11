namespace SlimMessageBus.Host;

public static class Retry
{
    private static readonly Random _random = new(); // NOSONAR

    public static async Task WithDelay(Func<CancellationToken, Task> operation, Func<Exception, int, bool> shouldRetry, TimeSpan? delay, TimeSpan? jitter = default, CancellationToken cancellationToken = default)
    {
        if (operation is null) throw new ArgumentNullException(nameof(operation));
        if (shouldRetry is null) throw new ArgumentNullException(nameof(shouldRetry));

        var attempt = 0;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await operation(cancellationToken);
                return;
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                if (!shouldRetry(e, attempt++))
                {
                    throw;
                }

                if (delay > TimeSpan.Zero)
                {
                    var randomJitter = jitter > TimeSpan.Zero
                        ? TimeSpan.FromMilliseconds(_random.NextDouble() * jitter.Value.TotalMilliseconds)
                        : TimeSpan.Zero;

                    await Task.Delay(delay.Value + randomJitter, cancellationToken);
                }
            }
        } while (true);
    }
}
