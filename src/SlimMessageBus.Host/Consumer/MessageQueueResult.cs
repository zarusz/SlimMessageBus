namespace SlimMessageBus.Host
{
    public class MessageQueueResult<TMessage> where TMessage : class
    {
        public bool Success { get; set; }
        public TMessage LastSuccessMessage { get; set; }
    }
}