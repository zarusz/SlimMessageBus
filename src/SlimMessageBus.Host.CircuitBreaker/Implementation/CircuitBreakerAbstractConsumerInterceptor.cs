namespace SlimMessageBus.Host.CircuitBreaker;

/// <summary>
/// Circuit breaker to toggle consumer status on an external events.
/// </summary>
internal sealed class CircuitBreakerAbstractConsumerInterceptor : IAbstractConsumerInterceptor
{
    public int Order => 100;

    public async Task<bool> CanStart(AbstractConsumer consumer)
    {
        var breakerTypes = consumer.Settings.SelectMany(x => x.GetOrDefault(ConsumerSettingsProperties.CircuitBreakerTypes, [])).ToHashSet();
        if (breakerTypes.Count == 0)
        {
            // no breakers, allow to pass
            return true;
        }

        var breakers = consumer.GetOrCreate(AbstractConsumerProperties.Breakers, () => [])!;

        async Task BreakerChanged(Circuit state)
        {
            if (!consumer.IsStarted)
            {
                return;
            }

            var isPaused = consumer.IsPaused();
            var shouldPause = state == Circuit.Closed || breakers.Exists(x => x.State == Circuit.Closed);
            if (shouldPause != isPaused)
            {
                var path = consumer.Path;
                var bus = consumer.Settings[0].MessageBusSettings.Name ?? "default";
                if (shouldPause)
                {
                    consumer.Logger.LogWarning("Circuit breaker tripped for '{Path}' on '{Bus}' bus. Consumer paused.", path, bus);
                    await consumer.DoStop().ConfigureAwait(false);
                }
                else
                {
                    consumer.Logger.LogInformation("Circuit breaker restored for '{Path}' on '{Bus}' bus. Consumer resumed.", path, bus);
                    await consumer.DoStart().ConfigureAwait(false);
                }
                consumer.SetIsPaused(shouldPause);
            }
        }

        var sp = consumer.Settings.Select(x => x.MessageBusSettings.ServiceProvider).First(x => x != null);
        foreach (var breakerType in breakerTypes)
        {
            var breaker = (IConsumerCircuitBreaker)ActivatorUtilities.CreateInstance(sp, breakerType, consumer.Settings);
            breakers.Add(breaker);

            await breaker.Subscribe(BreakerChanged);
        }

        var isPaused = breakers.Exists(x => x.State == Circuit.Closed);
        consumer.SetIsPaused(isPaused);
        return !isPaused;
    }

    public async Task<bool> CanStop(AbstractConsumer consumer)
    {
        var breakers = consumer.GetOrDefault(AbstractConsumerProperties.Breakers, null);
        if (breakers == null || breakers.Count == 0)
        {
            // no breakers, allow to pass
            return true;
        }

        foreach (var breaker in breakers)
        {
            breaker.Unsubscribe();

            if (breaker is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (breaker is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        breakers.Clear();

        return !consumer.IsPaused();
    }

    public Task Started(AbstractConsumer consumer) => Task.CompletedTask;

    public Task Stopped(AbstractConsumer consumer) => Task.CompletedTask;
}
