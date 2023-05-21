namespace SlimMessageBus.Host.AzureEventHub;

static internal class MessageBusSettingsExtensions
{
    static internal bool IsAnyConsumerDeclared(this MessageBusSettings settings) => settings.Consumers.Count > 0 || settings.RequestResponse != null;
}
