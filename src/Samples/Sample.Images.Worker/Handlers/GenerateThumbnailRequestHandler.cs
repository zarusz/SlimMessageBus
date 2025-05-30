﻿namespace Sample.Images.Worker.Handlers;

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

using Sample.Images.FileStore;
using Sample.Images.Messages;

using SlimMessageBus;

public class GenerateThumbnailRequestHandler(IFileStore fileStore, IThumbnailFileIdStrategy fileIdStrategy)
    : IRequestHandler<GenerateThumbnailRequest, GenerateThumbnailResponse>
{
    public async Task<GenerateThumbnailResponse> OnHandle(GenerateThumbnailRequest request, CancellationToken cancellationToken)
    {
        var image = await LoadImage(request.FileId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Image with id '{request.FileId}' does not exist");

        using (image)
        {
            var thumbnailFileId = fileIdStrategy.GetFileId(request.FileId, request.Width, request.Height, request.Mode);
            var thumbnail = ScaleToFitInside(image, request.Width, request.Height);

            using (thumbnail)
            {
                SaveImage(thumbnailFileId, thumbnail);

                return new GenerateThumbnailResponse
                {
                    FileId = thumbnailFileId
                };
            }
        }
    }

    private async Task<Image> LoadImage(string fileId)
    {
        var imageContent = await fileStore.GetFile(fileId).ConfigureAwait(false);
        if (imageContent == null)
        {
            return null;
        }
        using (imageContent)
        {
            var image = Image.FromStream(imageContent);
            return image;
        }
    }

    private void SaveImage(string fileId, Image image)
    {
        using (var ms = new MemoryStream())
        {
            image.Save(ms, ImageFormat.Jpeg);
            ms.Seek(0, SeekOrigin.Begin);

            fileStore.UploadFile(fileId, ms);
        }
    }

    private static Image ScaleToFitInside(Image imgPhoto, int targetW, int targetH)
    {
        // See https://www.codeproject.com/Articles/2941/Resizing-a-Photographic-image-with-GDI-for-NET

        var sourceX = 0;
        var sourceY = 0;
        var sourceWidth = imgPhoto.Width;
        var sourceHeight = imgPhoto.Height;

        var scaleW = targetW / (float)sourceWidth;
        var scaleH = targetH / (float)sourceHeight;
        var scale = Math.Min(scaleW, scaleH);

        var destX = 0;
        var destY = 0;
        var destWidth = (int)(sourceWidth * scale);
        var destHeight = (int)(sourceHeight * scale);

        var bmPhoto = new Bitmap(destWidth, destHeight, PixelFormat.Format24bppRgb);
        bmPhoto.SetResolution(imgPhoto.HorizontalResolution, imgPhoto.VerticalResolution);

        using (var grPhoto = Graphics.FromImage(bmPhoto))
        {
            grPhoto.InterpolationMode = InterpolationMode.HighQualityBicubic;

            grPhoto.DrawImage(imgPhoto,
                new Rectangle(destX, destY, destWidth, destHeight),
                new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                GraphicsUnit.Pixel);
        }

        return bmPhoto;
    }
}
