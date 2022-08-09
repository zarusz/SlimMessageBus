namespace SlimMessageBus.Host.Serialization.Benchmark;

public class AddCommand
{
    public string OperationId { get; set; }
    public int Left { get; set; }
    public int Right { get; set; }
}
