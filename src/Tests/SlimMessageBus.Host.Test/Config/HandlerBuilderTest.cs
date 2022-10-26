namespace SlimMessageBus.Host.Test.Config;

using SlimMessageBus.Host.Config;

public class HandlerBuilderTest
{
    [Fact]
    public void BuildsProperSettings()
    {
        // arrange
        var path = "topic";
        var settings = new MessageBusSettings();

        // act
        var subject = new HandlerBuilder<SomeRequest, SomeResponse>(settings)
            .Topic(path)
            .Instances(3)
            .WithHandler<SomeRequestMessageHandler>();

        // assert
        subject.ConsumerSettings.MessageType.Should().Be(typeof(SomeRequest));
        subject.MessageType.Should().Be(typeof(SomeRequest));
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