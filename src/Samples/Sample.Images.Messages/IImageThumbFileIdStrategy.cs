namespace Sample.Images.Messages;

public interface IThumbnailFileIdStrategy
{
    string GetFileId(string fileId, int w, int h, ThumbnailMode mode);
}
