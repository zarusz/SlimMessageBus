namespace SlimMessageBus.Host;

using Microsoft.Extensions.DependencyInjection.Extensions;

using SlimMessageBus.Host.Config;

public static class MessageBusBuilderExtensions
{
    /// <summary>
    /// Adds ASP.Net Core integrations for SlimMessageBus.
    /// </summary>
    /// <param name="mbb"></param>
    /// <returns></returns>
    public static MessageBusBuilder AddAspNet(this MessageBusBuilder mbb)
    {
        mbb.PostConfigurationActions.Add(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ICurrentMessageBusProvider, HttpContextAccessorCurrentMessageBusProvider>());
        });
        return mbb;
    }
}