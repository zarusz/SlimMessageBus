namespace SlimMessageBus.Host.Nats;

public class NatsMessageBusSettings : HasProviderExtensions
{
    public string Endpoint { get; set; }
    public string ClientName { get; set; }
    public NatsAuthOpts AuthOpts { get; set; }
}