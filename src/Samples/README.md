# Samples

These are some selected samples for SlimMessageBus.

- [Samples](#samples)
  - [Sample.DomainEvents](#sampledomainevents)
  - [Sample.Images](#sampleimages)
    - [Sequence diagram](#sequence-diagram)
    - [Key snippet](#key-snippet)
  - [Sample.Serialization](#sampleserialization)

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

Use case:

- Some front-end web app needs to display downsized image (thumbnails) of large images to speed up the page load.
- The thumbnails are requested in the WebApi and are generated on demand (and cached to disk) by the Worker (unless they exist already).
- WebApi and Worker exchange messages via Apache Kafka
- Worker can be scaled out (more instances, more Kafka partitions)

The sample project uses request-response to generate image thumbnails. It consists of two main applications:

- WebApi (ASP.NET WebApi)
- Worker (.NET Console App)

The WebApi serves thumbnails of an original image given the desired _Width x Height_. To request a thumbnail of size `120x80` of the image `DSC0843.jpg` use:

`https://localhost:56788/api/image/DSC3781.jpg/r/?w=120&h=80&mode=1`

The thumbnail generation happens on the Worker. Because the image resizing is an CPU/memory intensive operation, the number of workers can be scaled out as the load increases.

The original images and produced thumbnails cache reside on disk in the folder: `.\SlimMessageBus\src\Samples\Content\`

To obtain the original image use:

`https://localhost:56788/api/image/DSC3781.jpg`

When a thumbnail of the specified size already exists it will be served by WebApi, otherwise a request message is sent to Worker to perform processing. When the Worker generates the thumbnail it responds with a response message.

### Sequence diagram

![](images/SlimMessageBus_Sample_Images.png)

### Key snippet

The front-end web app makes a call to resize an image `DSC0862.jpg` to `120x80` resolution, by using this URL:

`https://localhost:56788/api/image/DSC3781.jpg/r/?w=120&h=80&mode=1`

This gets handled by the WebApi method of the `ImageController`

```cs
private readonly IRequestResponseBus _bus;
// ...
[Route("{fileId}")]
public async Task<HttpResponseMessage> GetImageThumbnail(string fileId, ThumbnailMode mode, int w, int h)
{
   var thumbFileContent = // ... try to load content for the desired thumbnail w/h/mode/fileId
   if (thumbFileContent == null)
   {
      // Task will await until response comes back (or timeout happens). The HTTP request will be queued and IIS processing thread released.
      var thumbGenResponse = await _bus.Send(new GenerateThumbnailRequest(fileId, mode, w, h));
      thumbFileContent = await _fileStore.GetFile(thumbGenResponse.FileId);
   }
   return ServeStream(thumbFileContent);
}
```

The `GenerateThumbnailRequest` request is handled by a handler in one of the pool of Worker console apps.

```cs
public class GenerateThumbnailRequestHandler : IRequestHandler<GenerateThumbnailRequest, GenerateThumbnailResponse>
{
   public Task<GenerateThumbnailResponse> OnHandle(GenerateThumbnailRequest request)
   {
      // some processing
      return new GenerateThumbnailResponse { FileId = thumbnailFileId };
   }
}
```

The response gets replied to the originating WebApi instance and the `Task<GenerateThumbnailResponse>` resolves causing the queued HTTP request to serve the resized image thumbnail.

```cs
var thumbGenResponse = await _bus.Send(new GenerateThumbnailRequest(fileId, mode, w, h));
```

The message bus configuration for the WebApi:

```cs
services.AddSlimMessageBus((mbb, svp) =>
{
   // unique id across instances of this application (e.g. 1, 2, 3)
   var instanceId = Configuration["InstanceId"];
   var kafkaBrokers = Configuration["Kafka:Brokers"];

   var instanceGroup = $"webapi-{instanceId}";
   var instanceReplyTo = $"webapi-{instanceId}-response";

   mbb
      .Produce<GenerateThumbnailRequest>(x =>
      {
         // Default response timeout for this request type
         //x.DefaultTimeout(TimeSpan.FromSeconds(10));
         x.DefaultTopic("thumbnail-generation");
      })
      .ExpectRequestResponses(x =>
      {
         x.ReplyToTopic(instanceReplyTo);
         x.KafkaGroup(instanceGroup);
         // Default global response timeout
         x.DefaultTimeout(TimeSpan.FromSeconds(30));
      })
      .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers));
   });
});
services.AddMessageBusJsonSerializer(();
services.AddMessageBusServicesFromAssembly(Assembly.GetExecutingAssembly());

services.AddHttpContextAccessor(); // This is required for the SlimMessageBus.Host.AspNetCore plugin
```

The message bus configuration for the Worker:

```cs
// This sample uses Autofac
var builder = new ContainerBuilder();

services.AddSlimMessageBus((mbb, svp) =>
{
   // unique id across instances of this application (e.g. 1, 2, 3)
   var instanceId = configuration["InstanceId"];
   var kafkaBrokers = configuration["Kafka:Brokers"];

   var instanceGroup = $"worker-{instanceId}";
   var sharedGroup = "workers";

   mbb
      .Handle<GenerateThumbnailRequest, GenerateThumbnailResponse>(s =>
      {
         s.Topic("thumbnail-generation", t =>
         {
            t.WithHandler<GenerateThumbnailRequestHandler>()
               .KafkaGroup(sharedGroup)
               .Instances(3);
         });
      })
      .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)
      {
         ConsumerConfig = (config) =>
         {
            config.StatisticsIntervalMs = 60000;
            config.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Latest;
         }
      });
});
```

Because topics are partitioned in Kafka, requests originating from WebApi instances will be distributed across all Worker instances. However, to fine tune this, message key providers should be configured (see Kafka provider wiki and samples).

## Sample.Serialization

The [Sample.Serialization.ConsoleApp](Sample.Serialization.ConsoleApp) is a simple console app that shows different serializer plugins and how to use them. Additionally, the [Sample.Serialization.MessagesAvro](Sample.Serialization.MessagesAvro) project has a sample Avro IDL/Contract from which C# message DTOs are generated.
