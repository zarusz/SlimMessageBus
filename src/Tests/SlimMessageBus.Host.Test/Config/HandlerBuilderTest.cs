namespace SlimMessageBus.Host.Test.Config;

using SlimMessageBus.Host.Config;

public class HandlerBuilderTest
{
    private readonly MessageBusSettings messageBusSettings;

    public HandlerBuilderTest()
    {
        messageBusSettings = new MessageBusSettings();
    }

    [Fact]
    public void Given_RequestAndResposeType_When_Configured_Then_MessageType_And_ResponseType_And_DefaultHandlerTypeSet_ProperlySet()
    {
        // arrange

        // act
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(messageBusSettings);

        // assert
        subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.RequestResponse);
        subject.ConsumerSettings.ConsumerType.Should().BeNull();
        subject.ConsumerSettings.MessageType.Should().Be(typeof(SomeRequest));
        subject.ConsumerSettings.ResponseType.Should().Be(typeof(SomeResponse));
    }

    [Fact]
    public void Given_Path_Set_When_Configured_Then_Path_ProperlySet()
    {
        // arrange
        var path = "topic";

        // act
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(messageBusSettings)
            .Path(path);

        // assert
        subject.ConsumerSettings.Path.Should().Be(path);
        subject.ConsumerSettings.PathKind.Should().Be(PathKind.Topic);
    }

    [Fact]
    public void BuildsProperSettings()
    {
        // arrange
        var path = "topic";

        // act
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(messageBusSettings)
            .Topic(path)
            .Instances(3)
            .WithHandler<SomeRequestMessageHandler>();

        // assert
        subject.ConsumerSettings.MessageType.Should().Be(typeof(SomeRequest));
        subject.ConsumerSettings.Path.Should().Be(path);
        subject.ConsumerSettings.Instances.Should().Be(3);
        subject.ConsumerSettings.ConsumerType.Should().Be(typeof(SomeRequestMessageHandler));
        subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.RequestResponse);
        subject.ConsumerSettings.IsRequestMessage.Should().BeTrue();
        subject.ConsumerSettings.ResponseType.Should().Be(typeof(SomeResponse));

        subject.ConsumerSettings.Invokers.Count.Should().Be(1);

        var consumerInvokerSettings = subject.ConsumerSettings.Invokers.Single(x => x.MessageType == typeof(SomeRequest));
        consumerInvokerSettings.MessageType.Should().Be(typeof(SomeRequest));
        consumerInvokerSettings.ConsumerType.Should().Be(typeof(SomeRequestMessageHandler));
        Func<Task> call = () => consumerInvokerSettings.ConsumerMethod(new SomeRequestMessageHandler(), new SomeRequest());
        call.Should().ThrowAsync<NotImplementedException>().WithMessage(nameof(SomeRequest));
    }
}