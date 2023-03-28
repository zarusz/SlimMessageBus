namespace SlimMessageBus.Host;

public interface IMessageBusSettingsPostProcessor
{
    void Run(MessageBusSettings settings);
}
