using System.IO;

namespace Sample.Images.Messages
{
    public class SimpleThumbnailFileIdStrategy : IThumbnailFileIdStrategy
    {
        #region Implementation of IImageThumbFileIdStrategy

        public string GetFileId(string fileId, int w, int h, ThumbnailMode mode)
        {
            var ext = Path.GetExtension(fileId);
            var name = Path.GetFileNameWithoutExtension(fileId);
            return $"{name}_{w}_{h}_{(int)mode}{ext}";
        }

        #endregion
    }
}