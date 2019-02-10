using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Moq;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Serialization;
using Xunit;

namespace SlimMessageBus.Host.AzureServiceBus.Test
{
    public class ServiceBusMessageBusTests : IDisposable
    {
        private ServiceBusMessageBusSettings ProviderBusSettings { get; }
        private Lazy<WrappedProviderMessageBus> ProviderBus { get; }
        private MessageBusBuilder BusBuilder { get; } = MessageBusBuilder.Create();
        private Mock<IQueueClient> QueueClientMock { get; } = new Mock<IQueueClient>();
        private Mock<ITopicClient> TopicClientMock { get; } = new Mock<ITopicClient>();

        public ServiceBusMessageBusTests()
        {
            BusBuilder.WithSerializer(new Mock<IMessageSerializer>().Object);
            BusBuilder.WithDependencyResolver(new Mock<IDependencyResolver>().Object);

            ProviderBusSettings = new ServiceBusMessageBusSettings("connection-string")
            {
                QueueClientFactory = (queue) => QueueClientMock.Object,
                TopicClientFactory = (topic) => TopicClientMock.Object
            };
            BusBuilder.WithProvider(mbSettings => new WrappedProviderMessageBus(mbSettings, ProviderBusSettings));
            ProviderBus = new Lazy<WrappedProviderMessageBus>(() => (WrappedProviderMessageBus)BusBuilder.Build());
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                ProviderBus.Value.Dispose();
            }
        }

        public class WrappedProviderMessageBus : ServiceBusMessageBus
        {
            public WrappedProviderMessageBus(MessageBusSettings settings, ServiceBusMessageBusSettings serviceBusSettings)
                : base(settings, serviceBusSettings)
            {
            }
        }

        public class SomeMessage
        {
            public string Id { get; set; }
            public int Value { get; set; }
        }

        [Fact]
        public async Task WhenPublishGivenModifierConfiguredForMessageTypeThenModifierExecuted()
        {
            // arrange
            BusBuilder.Produce<SomeMessage>(x =>
            {
                x.DefaultTopic("default-topic");
                x.WithModifier((message, sbMessage) =>
                {
                    sbMessage.MessageId = message.Id;
                    sbMessage.PartitionKey = (message.Value % 2).ToString(CultureInfo.InvariantCulture);
                });
            });

            var m1 = new SomeMessage { Id = "1", Value = 10 };
            var m2 = new SomeMessage { Id = "2", Value = 3 };

            // act
            await ProviderBus.Value.Publish(m1).ConfigureAwait(false);
            await ProviderBus.Value.Publish(m2).ConfigureAwait(false);

            // assert
            TopicClientMock.Verify(x => x.SendAsync(It.Is<Message>(m => m.MessageId == "1" && m.PartitionKey == "0")), Times.Once);
            TopicClientMock.Verify(x => x.SendAsync(It.Is<Message>(m => m.MessageId == "2" && m.PartitionKey == "1")), Times.Once);
        }

        [Fact]
        public async Task WhenPublishGivenModifierConfiguredForMessageTypeThatThrowsErrorThenModifierDoesNotPreventMessageDelivery()
        {
            // arrange
            BusBuilder.Produce<SomeMessage>(x =>
            {
                x.DefaultTopic("default-topic");
                x.WithModifier((message, sbMessage) => throw new Exception("Someone is having bad day today!"));
            });

            var m = new SomeMessage { Id = "1", Value = 10 };

            // act
            await ProviderBus.Value.Publish(m).ConfigureAwait(false);

            // assert
            TopicClientMock.Verify(x => x.SendAsync(It.IsAny<Message>()), Times.Once);
        }
    }
}
