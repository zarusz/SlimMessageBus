namespace SlimMessageBus.Host.AsyncApi;

using Microsoft.Extensions.DependencyInjection.Extensions;

using Saunter.Generation;

public static class MessageBusBuilderExtensions
{
    /// <summary>
    /// Tries to register the <see cref="IDocumentGenerator"/> for Saunter library to generate an AsyncAPI document from the SlimMessageBus bus definitions.
    /// </summary>
    /// <remarks>This needs to be run before the .AddAsyncApiSchemaGeneration() from Saunter</remarks>
    /// <param name="mbb"></param>
    /// <param name="busFilter">Provides a filter method to exclude or include a (child) bus from AsyncAPI document generation. When not provided will filter out bus named Memory</param>
    /// <returns></returns>
    public static MessageBusBuilder AddAsyncApiDocumentGenerator(this MessageBusBuilder mbb, Func<MessageBusSettings, bool>? busFilter = null)
    {
        mbb.Settings.Properties[BusFilter] = busFilter 
            ?? (x => x.Name != "Memory");
        
        mbb.PostConfigurationActions.Add(services =>
        {
            services.TryAddTransient<IDocumentGenerator, MessageBusDocumentGenerator>();
        });

        return mbb;
    }

    internal const string BusFilter = "AsyncAPI_BusFilter";
}
