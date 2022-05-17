namespace SlimMessageBus.Host.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using FluentAssertions;
    using Xunit;

    public class MessageWithHeadersSerializerTests
    {
        private readonly byte[] _payload;
        private readonly MessageWithHeadersSerializer _serializer;

        public MessageWithHeadersSerializerTests()
        {
            _payload = Encoding.UTF8.GetBytes("Sample message payload in UTF8");
            _serializer = new MessageWithHeadersSerializer(Encoding.UTF8);
        }

        [Fact]
        public void When_Deserialize_Given_WithoutHeaders_Then_SerializationWorks()
        {
            // arrange
            var m = new MessageWithHeaders(_payload);

            // act
            var payload = _serializer.Serialize(typeof(MessageWithHeaders), m);
            var m2 = (MessageWithHeaders)_serializer.Deserialize(typeof(MessageWithHeaders), payload);

            // assert
            m2.Headers.Count.Should().Be(0);
            _payload.SequenceEqual(m2.Payload).Should().BeTrue();
        }

        [Fact]
        public void When_Serialize_Given_WithoutPayload_Then_SerializationWorks()
        {
            // arrange
            var m = new MessageWithHeaders(null);

            // act
            var payload = _serializer.Serialize(typeof(MessageWithHeaders), m);
            var m2 = (MessageWithHeaders)_serializer.Deserialize(typeof(MessageWithHeaders), payload);

            // assert
            m2.Headers.Count.Should().Be(0);
            m2.Payload.Should().BeNull();
        }

        [Fact]
        public void When_Serialize_Given_Headers_Then_SerializationWorks()
        {
            var now = DateTime.Now.Ticks;

            // arrange
            var m = new MessageWithHeaders(_payload, new Dictionary<string, object>
                {
                    {"key1", "value1"},
                    {"key2", "value22"},
                    {"keyBool", true},
                    {"keyInt", 123},
                    {"keyLong", now},
                    {"keyNull", null},
                    {"keyGuid", Guid.Parse("{BF8F7690-CEC3-48F5-8C50-FA723BA18EB3}")},
                });

            // act
            var payload = _serializer.Serialize(typeof(MessageWithHeaders), m);
            var m2 = (MessageWithHeaders)_serializer.Deserialize(typeof(MessageWithHeaders), payload);

            // assert
            m2.Headers.Count.Should().Be(m.Headers.Count);
            m2.Headers.ContainsKey("key1").Should().BeTrue();
            m2.Headers["key1"].Should().Be("value1");
            m2.Headers.ContainsKey("key2").Should().BeTrue();
            m2.Headers["key2"].Should().Be("value22");
            m2.Headers["keyBool"].Should().Be(true);
            m2.Headers["keyInt"].Should().Be(123).And.BeOfType<int>();
            m2.Headers["keyLong"].Should().Be(now).And.BeOfType<long>();
            m2.Headers["keyNull"].Should().BeNull();
            m2.Headers["keyGuid"].Should().Be(Guid.Parse("{BF8F7690-CEC3-48F5-8C50-FA723BA18EB3}")).And.BeOfType<Guid>();

            _payload.SequenceEqual(m2.Payload).Should().BeTrue();
        }

        [Fact]
        public void When_Serialize_Given_LargeHeader_Then_SerializationWorks()
        {
            // arrange
            var largeValue = "System.InvalidOperationException: Image with id '_DSC0862.jpg' does not exist\r\n   at Sample.Images.Worker.Handlers.GenerateThumbnailRequestHandler.<OnHandle>d__3.MoveNext() in E:\\dev\\mygithub\\SlimMessageBus\\Samples\\Sample.Images.Worker\\Handlers\\GenerateThumbnailRequestHandler.cs:line 31\r\n--- End of stack trace from previous location where exception was thrown ---\r\n   at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task task)\r\n   at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task task)\r\n   at System.Runtime.CompilerServices.TaskAwaiter.GetResult()\r\n   at SlimMessageBus.Host.Kafka.TopicConsumerInstances.<ProcessMessage>d__13.MoveNext() in E:\\dev\\mygithub\\SlimMessageBus\\SlimMessageBus.Host.Kafka\\TopicSubscriberInstances.cs:line 109";
            var m = new MessageWithHeaders(_payload, new Dictionary<string, object>
            {
                ["key1"] = largeValue
            });


            // act
            var payload = _serializer.Serialize(typeof(MessageWithHeaders), m);
            var m2 = (MessageWithHeaders)_serializer.Deserialize(typeof(MessageWithHeaders), payload);

            // assert
            m2.Headers.Count.Should().Be(1);
            m2.Headers.ContainsKey("key1").Should().BeTrue();
            m2.Headers["key1"].Should().Be(largeValue);
        }
    }
}
