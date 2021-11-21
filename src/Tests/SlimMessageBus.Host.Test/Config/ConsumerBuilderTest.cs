namespace SlimMessageBus.Host.Test.Config
{
    using FluentAssertions;
    using SlimMessageBus.Host.Config;
    using System;
    using System.Threading.Tasks;
    using Xunit;

    public class ConsumerBuilderTest
    {
        private readonly MessageBusSettings messageBusSettings;

        public ConsumerBuilderTest()
        {
            messageBusSettings = new MessageBusSettings();
        }

        [Fact]
        public void Given_MessageType_When_Configured_Then_MessageType_ProperlySet()
        {
            // arrange

            // act
            var subject = new ConsumerBuilder<SomeMessage>(messageBusSettings);

            // assert
            subject.ConsumerSettings.MessageType.Should().Be(typeof(SomeMessage));
        }

        [Fact]
        public void Given_Path_Set_When_Configured_Then_Path_ProperlySet()
        {
            // arrange
            var path = "topic";

            // act
            var subject = new ConsumerBuilder<SomeMessage>(messageBusSettings)
                .Path(path);

            // assert
            subject.ConsumerSettings.Path.Should().Be(path);
            subject.ConsumerSettings.PathKind.Should().Be(PathKind.Topic);
        }

        [Fact]
        public void Given_Topic_Set_When_Configured_Then_Topic_ProperlySet()
        {
            // arrange
            var topic = "topic";

            // act
            var subject = new ConsumerBuilder<SomeMessage>(messageBusSettings)
                .Topic(topic);

            // assert
            subject.ConsumerSettings.Path.Should().Be(topic);
            subject.ConsumerSettings.PathKind.Should().Be(PathKind.Topic);
        }

        [Fact]
        public void Given_Instances_Set_When_Configured_Then_Instances_ProperlySet()
        {
            // arrange

            // act
            var subject = new ConsumerBuilder<SomeMessage>(messageBusSettings)
                .Instances(3);

            // assert
            subject.ConsumerSettings.Instances.Should().Be(3);
        }

        [Fact]
        public void Given_BaseMessageType_And_ItsHierarchy_When_WithConsumer_ForTheBaseTypeAndDerivedTypes_Then_TheConsumerSettingsAreCorrect()
        {
            // arrange
            var topic = "topic";

            // act
            var subject = new ConsumerBuilder<BaseMessage>(messageBusSettings)
                .Topic(topic)
                .WithConsumer<BaseMessageConsumer>()
                .WithConsumer<DerivedAMessageConsumer, DerivedAMessage>()
                .WithConsumer<DerivedBMessageConsumer, DerivedBMessage>()
                .WithConsumer<Derived2AMessageConsumer, Derived2AMessage>();

            // assert
            subject.ConsumerSettings.IsRequestMessage.Should().BeFalse();
            subject.ConsumerSettings.ResponseType.Should().BeNull();

            subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.Consumer);
            subject.ConsumerSettings.ConsumerType.Should().Be(typeof(BaseMessageConsumer));
            Func<Task> call = () => subject.ConsumerSettings.ConsumerMethod(new BaseMessageConsumer(), new BaseMessage(), topic);
            call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(BaseMessage));

            subject.ConsumerSettings.ConsumersByMessageType.Count.Should().Be(4);

            var consumerInvokerSettings = subject.ConsumerSettings.ConsumersByMessageType[typeof(BaseMessage)];
            consumerInvokerSettings.MessageType.Should().Be(typeof(BaseMessage));
            consumerInvokerSettings.ConsumerType.Should().Be(typeof(BaseMessageConsumer));
            call = () => consumerInvokerSettings.ConsumerMethod(new BaseMessageConsumer(), new BaseMessage(), topic);
            call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(BaseMessage));

            consumerInvokerSettings = subject.ConsumerSettings.ConsumersByMessageType[typeof(DerivedAMessage)];
            consumerInvokerSettings.MessageType.Should().Be(typeof(DerivedAMessage));
            consumerInvokerSettings.ConsumerType.Should().Be(typeof(DerivedAMessageConsumer));
            call = () => consumerInvokerSettings.ConsumerMethod(new DerivedAMessageConsumer(), new DerivedAMessage(), topic);
            call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(DerivedAMessage));

            consumerInvokerSettings = subject.ConsumerSettings.ConsumersByMessageType[typeof(DerivedBMessage)];
            consumerInvokerSettings.MessageType.Should().Be(typeof(DerivedBMessage));
            consumerInvokerSettings.ConsumerType.Should().Be(typeof(DerivedBMessageConsumer));
            call = () => consumerInvokerSettings.ConsumerMethod(new DerivedBMessageConsumer(), new DerivedBMessage(), topic);
            call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(DerivedBMessage));

            consumerInvokerSettings = subject.ConsumerSettings.ConsumersByMessageType[typeof(Derived2AMessage)];
            consumerInvokerSettings.MessageType.Should().Be(typeof(Derived2AMessage));
            consumerInvokerSettings.ConsumerType.Should().Be(typeof(Derived2AMessageConsumer));
            call = () => consumerInvokerSettings.ConsumerMethod(new Derived2AMessageConsumer(), new Derived2AMessage(), topic);
            call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(Derived2AMessage));
        }

        [Fact]
        public void Given_BaseRequestType_And_ItsHierarchy_When_WithConsumer_ForTheBaseTypeAndDerivedTypes_Then_TheConsumerSettingsAreCorrect()
        {
            // arrange
            var topic = "topic";

            // act
            var subject = new ConsumerBuilder<BaseRequest>(messageBusSettings)
                .Topic(topic)
                .WithConsumer<BaseRequestConsumer>()
                .WithConsumer<DerivedRequestConsumer, DerivedRequest>();

            // assert

            subject.ConsumerSettings.IsRequestMessage.Should().BeTrue();
            subject.ConsumerSettings.ResponseType.Should().Be(typeof(BaseResponse));

            subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.Consumer);
            subject.ConsumerSettings.ConsumerType.Should().Be(typeof(BaseRequestConsumer));
            Func<Task> call = () => subject.ConsumerSettings.ConsumerMethod(new BaseRequestConsumer(), new BaseRequest(), topic);
            call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(BaseRequest));

            subject.ConsumerSettings.ConsumersByMessageType.Count.Should().Be(2);

            var consumerInvokerSettings = subject.ConsumerSettings.ConsumersByMessageType[typeof(BaseRequest)];
            consumerInvokerSettings.MessageType.Should().Be(typeof(BaseRequest));
            consumerInvokerSettings.ConsumerType.Should().Be(typeof(BaseRequestConsumer));
            call = () => consumerInvokerSettings.ConsumerMethod(new BaseRequestConsumer(), new BaseRequest(), topic);
            call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(BaseRequest));

            consumerInvokerSettings = subject.ConsumerSettings.ConsumersByMessageType[typeof(DerivedRequest)];
            consumerInvokerSettings.MessageType.Should().Be(typeof(DerivedRequest));
            consumerInvokerSettings.ConsumerType.Should().Be(typeof(DerivedRequestConsumer));
            call = () => consumerInvokerSettings.ConsumerMethod(new DerivedRequestConsumer(), new DerivedRequest(), topic);
            call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(DerivedRequest));
        }

        public class BaseMessage
        {
        }

        public class DerivedAMessage : BaseMessage
        {
        }

        public class DerivedBMessage : BaseMessage
        {
        }

        public class Derived2AMessage : DerivedAMessage
        {
        }

        public class BaseMessageConsumer : IConsumer<BaseMessage>
        {
            public Task OnHandle(BaseMessage message, string path) => throw new NotImplementedException(nameof(BaseMessage));
        }

        public class DerivedAMessageConsumer : IConsumer<DerivedAMessage>
        {
            public Task OnHandle(DerivedAMessage message, string path) => throw new NotImplementedException(nameof(DerivedAMessage));
        }

        public class DerivedBMessageConsumer : IConsumer<DerivedBMessage>
        {
            public Task OnHandle(DerivedBMessage message, string path) => throw new NotImplementedException(nameof(DerivedBMessage));
        }

        public class Derived2AMessageConsumer : IConsumer<Derived2AMessage>
        {
            public Task OnHandle(Derived2AMessage message, string path) => throw new NotImplementedException(nameof(Derived2AMessage));
        }

        public class BaseResponse
        {
        }

        public class BaseRequest : IRequestMessage<BaseResponse>
        {
        }

        public class DerivedRequest : BaseRequest
        {
        }

        public class BaseRequestConsumer : IConsumer<BaseRequest>
        {
            public Task OnHandle(BaseRequest message, string path) => throw new NotImplementedException(nameof(BaseRequest));
        }

        public class DerivedRequestConsumer : IConsumer<DerivedRequest>
        {
            public Task OnHandle(DerivedRequest message, string path) => throw new NotImplementedException(nameof(DerivedRequest));
        }
    }
}