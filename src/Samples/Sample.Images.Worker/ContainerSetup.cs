using System.Collections.Generic;
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
using SlimMessageBus.Host.Kafka.Configs;
using Microsoft.Extensions.Logging;

namespace Sample.Images.Worker
{
    public static class ContainerSetup
    {
        public static IContainer Create(IConfigurationRoot configuration, ILoggerFactory loggerFactory)
        {
            var builder = new ContainerBuilder();

            Configure(builder, configuration, loggerFactory);

            var container = builder.Build();
            AutofacMessageBusDependencyResolver.Container = container;
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

        private static IMessageBus BuildMessageBus(IConfigurationRoot configuration, IComponentContext x)
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
                            .Group(sharedGroup)
                            .Instances(3);
                    });
                })
                .WithDependencyResolver(new AutofacMessageBusDependencyResolver(x.Resolve<ILogger<AutofacMessageBusDependencyResolver>>()))
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)
                {
                    ConsumerConfigFactory = group => new Dictionary<string, object>
                    {
                        {KafkaConfigKeys.ConsumerKeys.AutoCommitEnableMs, 5000},
                        {KafkaConfigKeys.ConsumerKeys.StatisticsIntervalMs, 60000},
                        {
                            "default.topic.config", new Dictionary<string, object>
                            {
                                {KafkaConfigKeys.ConsumerKeys.AutoOffsetReset, KafkaConfigValues.AutoOffsetReset.Latest}
                            }
                        }
                    }
                });

            var messageBus = messageBusBuilder.Build();
            return messageBus;
        }
    }
}