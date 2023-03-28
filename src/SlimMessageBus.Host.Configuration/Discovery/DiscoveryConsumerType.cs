namespace SlimMessageBus.Host;

public class DiscoveryConsumerType
{
    public Type ConsumerType { get; set; }
    public Type InterfaceType { get; set; }
    public Type MessageType { get; set; }
    public Type ResponseType { get; set; }
}