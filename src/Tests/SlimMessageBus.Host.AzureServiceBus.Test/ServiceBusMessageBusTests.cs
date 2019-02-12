using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using FluentAssertions;
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

        private IDictionary<string, Mock<IQueueClient>> QueueClientMockByName { get; } = new ConcurrentDictionary<string, Mock<IQueueClient>>();
        private IDictionary<string, Mock<ITopicClient>> TopicClientMockByName { get; } = new ConcurrentDictionary<string, Mock<ITopicClient>>();

        public ServiceBusMessageBusTests()
        {
            BusBuilder.WithSerializer(new Mock<IMessageSerializer>().Object);
            BusBuilder.WithDependencyResolver(new Mock<IDependencyResolver>().Object);

            ProviderBusSettings = new ServiceBusMessageBusSettings("connection-string")
            {
                QueueClientFactory = queue =>
                {
                    var m = new Mock<IQueueClient>();
                    QueueClientMockByName.Add(queue, m);
                    return m.Object;
                },
                TopicClientFactory = topic =>
                {
                    var m = new Mock<ITopicClient>();
                    TopicClientMockByName.Add(topic, m);
                    return m.Object;
                }
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
                if (ProviderBus.IsValueCreated)
                {
                    ProviderBus.Value.Dispose();
                }
            }
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
            var topicClient = TopicClientMockByName["default-topic"];
            topicClient.Verify(x => x.SendAsync(It.Is<Message>(m => m.MessageId == "1" && m.PartitionKey == "0")), Times.Once);
            topicClient.Verify(x => x.SendAsync(It.Is<Message>(m => m.MessageId == "2" && m.PartitionKey == "1")), Times.Once);
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
            TopicClientMockByName["default-topic"].Verify(x => x.SendAsync(It.IsAny<Message>()), Times.Once);
        }

        [Fact]
        public void WhenCreateGivenSameMessageTypeConfiguredTwiceForTopicAndForQueueThenConfigurationExceptionThrown()
        {
            // arrange
            BusBuilder.Produce<SomeMessage>(x => x.ToTopic());
            BusBuilder.Produce<SomeMessage>(x => x.ToQueue());

            // act
            Func<IMessageBus> creation = () => BusBuilder.Build();

            // assert
            creation.Should().Throw<InvalidConfigurationMessageBusException>()
                .WithMessage($"The produced message type '{typeof(SomeMessage).FullName}' was declared more than once*");
        }

        [Fact]
        public void WhenCreateGivenSameDefaultNameUsedForTopicAndForQueueThenConfigurationExceptionThrown()
        {
            // arrange
            const string name = "the-same-name";
            BusBuilder.Produce<SomeMessage>(x => x.DefaultTopic(name));
            BusBuilder.Produce<OtherMessage>(x => x.DefaultQueue(name));

            // act
            Func<IMessageBus> creation = () => BusBuilder.Build();

            // assert
            creation.Should().Throw<InvalidConfigurationMessageBusException>()
                .WithMessage($"The same name '{name}' was used for queue and topic*");
        }

        [Fact]
        public async Task WhenPublishTopicClientOrQueueClientIsCreatedForTopicNameOrQueueName()
        {
            // arrange
            BusBuilder.Produce<SomeMessage>(x => x.ToTopic());
            BusBuilder.Produce<OtherMessage>(x => x.ToQueue());

            var sm1 = new SomeMessage { Id = "1", Value = 10 };
            var sm2 = new SomeMessage { Id = "2", Value = 12 };
            var om1 = new OtherMessage { Id = "1" };
            var om2 = new OtherMessage { Id = "2" };

            // act
            await ProviderBus.Value.Publish(sm1, "some-topic").ConfigureAwait(false);
            await ProviderBus.Value.Publish(sm2, "some-topic").ConfigureAwait(false);
            await ProviderBus.Value.Publish(om1, "some-queue").ConfigureAwait(false);
            await ProviderBus.Value.Publish(om2, "some-queue").ConfigureAwait(false);

            // assert
            TopicClientMockByName.Should().ContainKey("some-topic");
            TopicClientMockByName.Should().HaveCount(1);
            QueueClientMockByName.Should().ContainKey("some-queue");
            QueueClientMockByName.Should().HaveCount(1);
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

    public class OtherMessage
    {
        public string Id { get; set; }
    }
}
