using SlimMessageBus;

namespace Sample.Images.Messages
{
    public class GenerateThumbnailResponse : IResponseMessage
    {
        public string RequestId { get; set; }
        public string FileId { get; set; }
    }
}