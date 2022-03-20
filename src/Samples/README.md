## Sample.Simple.ConsoleApp

This is the simplest, one-class code example.
It shows how it is easy to change providers in one place while having the rest of the code intact.

## Sample.DomainEvents

This sample shows how `SlimMessageBus` can be used to implement domain events.

`Sample.DomainEvents.Domain` project is the domain model that has the `OrderSubmittedEvent`. The domain layer has a dependency on `SlimMessageBus` to be able to publish domain events.

`Sample.DomainEvents.Application` implements the application logic and has the handler `OrderSubmittedHandler` for the domain event (which implements the `IConsumer<OrderSubmittedEvent>`).

`Sample.DomainEvents.WebApi` project is an ASP.NET Core 5.0 project that configures the `SlimMessageBus.Host.Memory` to enable in-process message passing.
Notice that the `MessageBus.Current` will resolve the `IMessageBus` instance from the current web request scope. Each handler instance will be scoped to the web request as well.
The MessageBus instance is a web request scoped. The scope could as well be a singleton.

Run the WebApi project and POST (without any payload) to `https://localhost:5001/api/orders`. An order will be submitted:

```text
2018-12-09 23:06:34.4667|INFO|Sample.DomainEvents.Domain.OrderSubmittedHandler|Customer John Whick just placed an order for:
2018-12-09 23:06:34.4667|INFO|Sample.DomainEvents.Domain.OrderSubmittedHandler|- 2x id_machine_gun
2018-12-09 23:06:34.4749|INFO|Sample.DomainEvents.Domain.OrderSubmittedHandler|- 4x id_grenade
2018-12-09 23:06:34.4749|INFO|Sample.DomainEvents.Domain.OrderSubmittedHandler|Generating a shipping order...
```

## Sample.Images

The sample project uses request-response to generate image thumbnails. It consists of two main applications:

* WebApi (ASP.NET Core 3.1 WebApi)
* Worker (.NET Core 3.1 Console App)

The WebApi serves thumbnails of an original image given the desired *Width x Height*. To request a thumbnail of size `120x80` of the image `DSC0843.jpg` use:

`https://localhost:56788/api/image/DSC3781.jpg/r/?w=120&h=80&mode=1`

The thumbnail generation happens on the Worker. Because the image resizing is an CPU/memory intensive operation, the number of workers can be scaled out as the load increases.

The original images and produced thumbnails cache reside on disk in the folder: `.\SlimMessageBus\src\Samples\Content\`

To obtain the original image use:

`https://localhost:56788/api/image/DSC3781.jpg`

When a thumbnail of the specified size already exists it will be served by WebApi, otherwise a request message is sent to Worker to perform processing. When the Worker generates the thumbnail it responds with a response message.

**Sequence diagram**

![](images/SlimMessageBus_Sample_Images.png)

**Key snippet**

The `ImageController` has a method that serves thumbnails. Note the 8th line which is async and resolves when the Worker responds:
```cs
        [HttpGet("{fileId}/r")]
        public async Task<ActionResult> GetImageThumbnail(string fileId, [FromQuery] ThumbnailMode mode, [FromQuery] int w, [FromQuery] int h, CancellationToken cancellationToken)
        {
            var thumbFileId = _fileIdStrategy.GetFileId(fileId, w, h, mode);

            var thumbFileContent = await _fileStore.GetFile(thumbFileId);
            if (thumbFileContent == null)
            {
                try
                {
                    var thumbGenResponse = await _bus.Send(new GenerateThumbnailRequest(fileId, mode, w, h), cancellationToken);
                    thumbFileContent = await _fileStore.GetFile(thumbGenResponse.FileId);
                }
                catch (RequestHandlerFaultedMessageBusException)
                {
                    // The request handler for GenerateThumbnailRequest failed
                    return NotFound();
                }
                catch (OperationCanceledException)
                {
                    // The request was cancelled (HTTP connection cancelled, or request timed out)
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "The request was cancelled");
                }
            }

            return ServeStream(thumbFileContent);
        }
```

The `GenerateThumbnailRequestHandler` handles the resizing operation on the Worker side:
```cs
    public class GenerateThumbnailRequestHandler : IRequestHandler<GenerateThumbnailRequest, GenerateThumbnailResponse>
    {
        private readonly IFileStore _fileStore;
        private readonly IThumbnailFileIdStrategy _fileIdStrategy;

		// ...
		
        public async Task<GenerateThumbnailResponse> OnHandle(GenerateThumbnailRequest request, string topic)
        {
            var image = await LoadImage(request.FileId);
            if (image == null)
            {
                // Note: This will cause RequestHandlerFaultedMessageBusException thrown on the other side (IRequestResponseBus.Send() method)
                throw new InvalidOperationException($"Image with id '{request.FileId}' does not exist");
            }
            using (image)
            {
                var thumbnailFileId = _fileIdStrategy.GetFileId(request.FileId, request.Width, request.Height, request.Mode);
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

		// ...
	}	

```

## Sample.Serialization

The [Sample.Serialization.ConsoleApp](Sample.Serialization.ConsoleApp) is a simple console app that shows different serializer plugins and how to use them. Additionally, the [Sample.Serialization.MessagesAvro](Sample.Serialization.MessagesAvro) project has a sample Avro IDL/Contract from which C# message DTOs are generated.