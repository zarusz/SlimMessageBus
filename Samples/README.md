## Sample.Images

Sample project that uses request-response to generate image thumbnails. It consists of two main applications:
* WebApi (ASP.NET WebApi)
* Worker (Console App)

The WebApi serves thumbnails of the desired *Width x Height* from an full size picture. To request a thumbnail of size `120x80` of the image `DSC0843.jpg` use:

`http://localhost:50452/api/Image/DSC3781.jpg/?w=120&h=80&mode=1`

The thumbnail generation happens on the Worker.

The images and produced thumbnails reside on disk in folder: `.\SlimMessageBus\Samples\Content\`

When a thumbnail of the specified size already exists it will be served by WebApi, otherwise a request message is sent to Worker to perform processing. When the Worker generates the thumbnail it responds with a response message.

**Sequence diagram**

![](images/SlimMessageBus_Sample_Images.png)


**Key snippet**

The `ImageController` has a method that serves thumbnails. Note the 8th line which is async and resolves when the Worker responds:
```cs
  public async Task<HttpResponseMessage> GetImageThumbnail(string fileId, ThumbnailMode mode, int w, int h)
  {
      var thumbFileId = _fileIdStrategy.GetFileId(fileId, w, h, mode);

      var thumbFileContent = await _fileStore.GetFile(thumbFileId);
      if (thumbFileContent == null)
      {
          var thumbGenResponse = await _bus.Send(new GenerateThumbnailRequest(fileId, mode, w, h));

          thumbFileContent = await _fileStore.GetFile(thumbGenResponse.FileId);
      }

      return ServeStream(thumbFileContent);
  }
```
