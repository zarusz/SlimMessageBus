namespace SlimMessageBus.Host;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds ASP.Net Core integrations for SlimMessageBus.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddMessageBusAspNet(this IServiceCollection services)
    {
        services.RemoveAll<ICurrentMessageBusProvider>();
        services.AddSingleton<ICurrentMessageBusProvider, HttpContextAccessorCurrentMessageBusProvider>();
        return services;
    }
}