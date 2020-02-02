using SlimMessageBus;

namespace Sample.AvroSer.Messages.CodeFirst
{
    public class DivideRequest : IRequestMessage<DivideResponse>
    {
        public string OperationId { get; set; }
        public int Left { get; set; }
        public int Right { get; set; }
    }
}
