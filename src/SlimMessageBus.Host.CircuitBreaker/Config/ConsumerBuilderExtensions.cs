namespace SlimMessageBus.Host.CircuitBreaker;

using Microsoft.Extensions.DependencyInjection.Extensions;

public static class ConsumerBuilderExtensions
{
    public static T AddConsumerCircuitBreakerType<T, TConsumerCircuitBreaker>(this T builder)
        where T : AbstractConsumerBuilder
        where TConsumerCircuitBreaker : IConsumerCircuitBreaker
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        var breakersTypes = builder.ConsumerSettings.GetOrCreate(ConsumerSettingsProperties.CircuitBreakerTypes, () => []);
        breakersTypes.TryAdd<TConsumerCircuitBreaker>();

        builder.PostConfigurationActions.Add(services =>
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IAbstractConsumerInterceptor, CircuitBreakerAbstractConsumerInterceptor>());
        });

        return builder;
    }
}