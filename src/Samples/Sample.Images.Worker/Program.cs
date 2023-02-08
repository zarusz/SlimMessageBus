namespace Sample.Images.Worker;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Sample.Images.FileStore;
using Sample.Images.FileStore.Disk;
using Sample.Images.Messages;

using Sample.Images.Worker.Handlers;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Kafka;
using SlimMessageBus.Host.Serialization.Json;

public class Program
{
    public static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .ConfigureServices((ctx, services) =>
            {
                var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\..\Content");
                services.AddSingleton<IFileStore>(x => new DiskFileStore(imagesPath));
                services.AddSingleton<IThumbnailFileIdStrategy, SimpleThumbnailFileIdStrategy>();

                // SlimMessageBus
                services.AddSlimMessageBus((mbb, svp) =>
                {
                    // unique id across instances of this application (e.g. 1, 2, 3)
                    var instanceId = ctx.Configuration["InstanceId"];
                    var kafkaBrokers = ctx.Configuration["Kafka:Brokers"];

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
                        .WithSerializer(new JsonMessageSerializer())
                        .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)
                        {
                            ConsumerConfig = (config) =>
                            {
                                config.StatisticsIntervalMs = 60000;
                                config.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Latest;
                            }
                        });
                }, addConsumersFromAssembly: new[] { typeof(GenerateThumbnailRequestHandler).Assembly });
            })
            .Build()
            .RunAsync();
    }
}
