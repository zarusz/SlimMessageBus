namespace SlimMessageBus.Host;

public class SendContext : ProducerContext
{
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset Expires { get; set; }
    public string RequestId { get; set; }
}
