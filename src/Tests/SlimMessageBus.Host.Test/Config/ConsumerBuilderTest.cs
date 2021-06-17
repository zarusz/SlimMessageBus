﻿namespace SlimMessageBus.Host.Test.Config
{
    using FluentAssertions;
    using SlimMessageBus.Host.Config;
    using Xunit;

    public class ConsumerBuilderTest
    {
        [Fact]
        public void BuildsProperSettings()
        {
            // arrange
            var topic = "topic";
            var settings = new MessageBusSettings();            

            // act
            var subject = new ConsumerBuilder<SomeMessage>(settings)
                .Topic(topic)
                .Instances(3)
                .WithConsumer<SomeMessageConsumer>();

            // assert
            subject.ConsumerSettings.MessageType.Should().Be(typeof(SomeMessage));
            subject.MessageType.Should().Be(typeof(SomeMessage));
            subject.Path.Should().Be(topic);
            subject.ConsumerSettings.Path.Should().Be(topic);
            subject.ConsumerSettings.Instances.Should().Be(3);
            subject.ConsumerSettings.ConsumerType.Should().Be(typeof(SomeMessageConsumer));
            subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.Consumer);
            subject.ConsumerSettings.IsRequestMessage.Should().BeFalse();
            subject.ConsumerSettings.ResponseType.Should().BeNull();
        }
    }
}