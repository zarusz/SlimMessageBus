using System;   
using System.Configuration;
using Autofac;
using Autofac.Extras.CommonServiceLocator;
using Microsoft.Practices.ServiceLocation;
using Sample.Images.FileStore;
using Sample.Images.FileStore.Disk;
using Sample.Images.Messages;
using Sample.Images.Worker.Handlers;
using SlimMessageBus;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.ServiceLocator;
using SlimMessageBus.Provider.Kafka;

namespace Sample.Images.Worker
{
    public class ContainerSetup
    {
        public static IContainer Create()
        {
            var builder = new ContainerBuilder();

            Configure(builder);

            var container = builder.Build();

            // Set the service locator to an AutofacServiceLocator.
            var csl = new AutofacServiceLocator(container);
            ServiceLocator.SetLocatorProvider(() => csl);

            return container;
        }

        private static void Configure(ContainerBuilder builder)
        {
            builder.RegisterType<DiskFileStore>().As<IFileStore>().SingleInstance();
            builder.RegisterType<SimpleThumbnailFileIdStrategy>().As<IThumbnailFileIdStrategy>().SingleInstance();

            // SlimMessageBus
            builder.Register(x => BuildMessageBus())
                .AsImplementedInterfaces()
                .SingleInstance();

            builder.RegisterType<GenerateThumbnailRequestHandler>().AsSelf();
            //builder.RegisterType<GenerateThumbnailRequestSubscriber>().AsSelf();
        }

        private static IMessageBus BuildMessageBus()
        {
            // unique id across instances of this application (e.g. 1, 2, 3)
            var instanceId = ConfigurationManager.AppSettings["InstanceId"];
            var kafkaBrokers = ConfigurationManager.AppSettings["Kafka.Brokers"];

            var instanceGroup = $"worker-{instanceId}";
            var sharedGroup = $"workers";

            var messageBusBuilder = new MessageBusBuilder()
                // ToDo: ReplyTo<>
                .SubscribeTo<GenerateThumbnailRequest>(s =>
                {
                    s.Topic("thumbnail-generation", t =>
                    {
                        t.Group(sharedGroup)
                            .WithConsumer<GenerateThumbnailRequestHandler>()
                            .Instances(3);

                        //t.Group(sharedGroup)
                        //    .WithConsumer<GenerateThumbnailRequestSubscriber>()
                        //    .Instances(3);
                    });
                })
                .WithDependencyResolverAsServiceLocator()
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers));

            var messageBus = messageBusBuilder.Build();
            return messageBus;
        }
    }
}