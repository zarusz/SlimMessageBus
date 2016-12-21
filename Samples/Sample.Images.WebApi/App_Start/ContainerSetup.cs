using Autofac;
using Sample.Images.FileStore;
using Sample.Images.FileStore.Disk;
using Sample.Images.Messages;
using SlimMessageBus;
using SlimMessageBus.Kafka;

namespace Sample.Images.WebApi
{
    public class ContainerSetup
    {


        public static void Configure(ContainerBuilder builder)
        {
            builder.RegisterType<DiskFileStore>().As<IFileStore>().SingleInstance();
            builder.RegisterType<SimpleThumbnailFileIdStrategy>().As<IThumbnailFileIdStrategy>().SingleInstance();

            // SlimMessageBus
            builder.RegisterType<KafkaMessageBus>().As<IRequestResponseBus>().SingleInstance();
        }
    }
}