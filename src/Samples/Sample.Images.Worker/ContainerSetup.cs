namespace Sample.Images.Worker
{
    using System.IO;
    using Autofac;
    using Sample.Images.FileStore;
    using Sample.Images.FileStore.Disk;
    using Sample.Images.Messages;
    using Sample.Images.Worker.Handlers;
    using SlimMessageBus;
    using SlimMessageBus.Host.Autofac;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.Serialization.Json;
    using SlimMessageBus.Host.Kafka;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public static class ContainerSetup
    {
        public static IContainer Create(IConfigurationRoot configuration, ILoggerFactory loggerFactory)
        {
            var builder = new ContainerBuilder();
            
            Configure(builder, configuration, loggerFactory);
            
            var container = builder.Build();
            return container;
        }

        private static void Configure(ContainerBuilder builder, IConfigurationRoot configuration, ILoggerFactory loggerFactory)
        {
            builder.RegisterInstance(loggerFactory)
                .As<ILoggerFactory>();

            builder.RegisterGeneric(typeof(Logger<>))
                .As(typeof(ILogger<>))
                .SingleInstance();

            var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\..\Content");
            builder.Register(x => new DiskFileStore(imagesPath)).As<IFileStore>().SingleInstance();
            builder.RegisterType<SimpleThumbnailFileIdStrategy>().As<IThumbnailFileIdStrategy>().SingleInstance();

            // SlimMessageBus
            builder.Register(x => BuildMessageBus(configuration, x))
                .AsImplementedInterfaces()
                .SingleInstance();

            builder.RegisterType<GenerateThumbnailRequestHandler>().AsSelf();
        }

        private static IMessageBus BuildMessageBus(IConfigurationRoot configuration, IComponentContext container)
        {
            // unique id across instances of this application (e.g. 1, 2, 3)
            var instanceId = configuration["InstanceId"];
            var kafkaBrokers = configuration["Kafka:Brokers"];

            var instanceGroup = $"worker-{instanceId}";
            var sharedGroup = "workers";

            var messageBusBuilder = MessageBusBuilder
                .Create()
                .Handle<GenerateThumbnailRequest, GenerateThumbnailResponse>(s =>
                {
                    s.Topic("thumbnail-generation", t =>
                    {
                        t.WithHandler<GenerateThumbnailRequestHandler>()
                            .KafkaGroup(sharedGroup)
                            .Instances(3);
                    });
                })
                .WithDependencyResolver(new AutofacMessageBusDependencyResolver(container))
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)
                {
                    ConsumerConfig = (config) =>
                    {
                        config.StatisticsIntervalMs = 60000;
                        config.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Latest;
                    }
                });

            var messageBus = messageBusBuilder.Build();
            return messageBus;
        }
    }
}