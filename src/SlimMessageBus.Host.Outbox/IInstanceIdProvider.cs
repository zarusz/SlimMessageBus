namespace SlimMessageBus.Host.Outbox;

public interface IInstanceIdProvider
{
    string GetInstanceId();
}
