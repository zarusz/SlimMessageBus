namespace SlimMessageBus.Host.Outbox;

using SlimMessageBus.Host;

public static class SettingsExtensions
{
    public static bool IsOutboxEnabled(this ProducerSettings producerSettings, MessageBusSettings messageBusSettings) =>
        producerSettings.GetOrDefault<bool>(BuilderExtensions.PropertyOutboxEnabled, messageBusSettings);

    public static bool IsTransactionScopeEnabled(this ConsumerSettings consumerSettings, MessageBusSettings messageBusSettings) =>
        consumerSettings.GetOrDefault<bool>(BuilderExtensions.PropertyTransactionScopeEnabled, messageBusSettings);
}
