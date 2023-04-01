namespace SlimMessageBus.Host.FluentValidation;

using SlimMessageBus.Host;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder AddFluentValidation(this MessageBusBuilder mbb, Action<FluentValidationMessageBusBuilder> configure)
    {
        if (mbb is null) throw new ArgumentNullException(nameof(mbb));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var pluginBuilder = new FluentValidationMessageBusBuilder(mbb);
        configure(pluginBuilder);

        return mbb;
    }
}
