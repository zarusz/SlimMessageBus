using System;
using System.Configuration;
using Autofac;
using Autofac.Extras.CommonServiceLocator;
using Microsoft.Practices.ServiceLocation;
using Sample.Images.FileStore;
using Sample.Images.FileStore.Disk;
using Sample.Images.Messages;
using SlimMessageBus;
using SlimMessageBus.Config;
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
            var messageBus = BuildMessageBus();
            builder.RegisterInstance(messageBus)
                .As<IPublishBus>()
                .As<IRequestResponseBus>();
        }

        private static IMessageBus BuildMessageBus()
        {
            // unique id across instances of this application (e.g. 1, 2, 3)
            var instanceId = ConfigurationManager.AppSettings["InstanceId"];
            var kafkaBrokers = ConfigurationManager.AppSettings["Kafka.Brokers"];

            var messageBusBuilder = new MessageBusBuilder()
                .Publish<GenerateThumbnailRequest>(x =>
                {
                    x.OnTopicByDefault("thumbnail-generation");
                })
                .SubscribeTo<GenerateThumbnailRequest>(x =>
                {
                    x.OnTopic("thumbnail-gneration");
                    //s.WithGroup("workers").Of(3);
                })
                .ExpectRequestResponses(x =>
                {
                    x.OnTopic($"worker-{instanceId}-response");
                    x.DefaultTimeout(TimeSpan.FromSeconds(10));
                })
                .WithSubscriberResolverAsServiceLocator()
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers));

            var messageBus = messageBusBuilder.Build();
            return messageBus;
        }
    }
}