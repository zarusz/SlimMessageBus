namespace SlimMessageBus.Host.FluentValidation;

using SlimMessageBus.Host;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder AddFluentValidation(this MessageBusBuilder mbb, Action<FluentValidationMessageBusBuilder> configuration)
    {
        if (configuration is null) throw new ArgumentNullException(nameof(configuration));

        var pluginBuilder = new FluentValidationMessageBusBuilder(mbb);
        configuration(pluginBuilder);

        return mbb;
    }
}
