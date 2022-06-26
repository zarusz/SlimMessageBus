namespace Sample.Images.Messages;

using SlimMessageBus;

public class GenerateThumbnailRequest : IRequestMessage<GenerateThumbnailResponse>
{
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