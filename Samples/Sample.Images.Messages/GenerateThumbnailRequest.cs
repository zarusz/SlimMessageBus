using SlimMessageBus;

namespace Sample.Images.Messages
{
    public class GenerateThumbnailRequest : IRequestMessageWithResponse<GenerateThumbnailResponse>
    {
        public string RequestId { get; set; }
        public string FileId { get; set; }
        public ThumbnailMode Mode { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public GenerateThumbnailRequest()
        {
        }

        public GenerateThumbnailRequest(string fileId, ThumbnailMode mode, int width, int height)
        {
            FileId = fileId;
            Mode = mode;
            Width = width;
            Height = height;
        }
    }
}