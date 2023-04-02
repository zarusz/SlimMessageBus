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
    /// <returns></returns>
    public static MessageBusBuilder AddAsyncApiDocumentGenerator(this MessageBusBuilder mbb)
    {
        mbb.PostConfigurationActions.Add(services =>
        {
            services.TryAddTransient<IDocumentGenerator, MessageBusDocumentGenerator>();
        });

        return mbb;
    }
}
