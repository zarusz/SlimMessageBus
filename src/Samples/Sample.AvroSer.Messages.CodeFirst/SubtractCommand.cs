namespace Sample.AvroSer.Messages.CodeFirst
{
    public class SubtractCommand
    {
        public string OperationId { get; set; }
        public int Left { get; set; }
        public int Right { get; set; }
    }
}