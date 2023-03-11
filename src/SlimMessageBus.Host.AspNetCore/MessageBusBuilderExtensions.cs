namespace SlimMessageBus.Host;

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
        if (mbb.Services is not null)
        {
            mbb.Services.RemoveAll<ICurrentMessageBusProvider>();
            mbb.Services.AddSingleton<ICurrentMessageBusProvider, HttpContextAccessorCurrentMessageBusProvider>();
        }
        return mbb;
    }
}