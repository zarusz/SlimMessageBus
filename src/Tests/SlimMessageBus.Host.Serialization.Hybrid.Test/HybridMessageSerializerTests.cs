namespace SlimMessageBus.Host.Serialization.Hybrid.Test
{

    public class HybridMessageSerializerTests
    {
        [Fact]
        public void When_ConstructorReceivesARepeatedDefinition_Then_ThrowException()
        {
            // arrange
            var mockDefaultSerializer = new Mock<IMessageSerializer>();
            var mockSerializer1 = new Mock<IMessageSerializer>();
            var mockSerializer2 = new Mock<IMessageSerializer>();
            var mockLogger = new Mock<ILogger<HybridMessageSerializer>>();

            var serializers = new Dictionary<IMessageSerializer, Type[]>
            {
                { mockSerializer1.Object, new[] { typeof(SampleOne), typeof(SampleTwo) } },
                { mockSerializer2.Object, new[] { typeof(SampleOne) } },
            };

            // act
            var act = () => new HybridMessageSerializer(mockLogger.Object, serializers, mockDefaultSerializer.Object);

            // assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void When_NoDefaultSerializerIsSupplied_Then_UseFirstSpecialist()
        {
            // arrange
            var mockDefaultSerializer = new Mock<IMessageSerializer>();
            var mockSerializer1 = new Mock<IMessageSerializer>();
            var mockLogger = new Mock<ILogger<HybridMessageSerializer>>();

            var serializers = new Dictionary<IMessageSerializer, Type[]>
            {
                { mockDefaultSerializer.Object, new[] { typeof(SampleOne) } },
                { mockSerializer1.Object, new[] { typeof(SampleTwo) } },
            };

            // act
            var target = new HybridMessageSerializer(mockLogger.Object, serializers, default);
            var actual = target.DefaultSerializer;

            // assert
            actual.Should().BeEquivalentTo(mockDefaultSerializer.Object);
        }

        [Fact]
        public void When_ASpecialisedMessageIsSerialized_Then_UseSpecializedSerializer()
        {
            // arrange
            var mockDefaultSerializer = new Mock<IMessageSerializer>();

            var mockSerializer1 = new Mock<IMessageSerializer>();
            mockSerializer1.Setup(x => x.Serialize(typeof(SampleOne), It.IsAny<SampleOne>(), It.IsAny<IMessageContext>())).Verifiable(Times.Once());

            var mockSerializer2 = new Mock<IMessageSerializer>();

            var mockLogger = new Mock<ILogger<HybridMessageSerializer>>();
            var serializers = new Dictionary<IMessageSerializer, Type[]>
            {
                { mockSerializer1.Object, new[] { typeof(SampleOne) } },
                { mockSerializer2.Object, new[] { typeof(SampleTwo) } },
            };

            // act
            var target = new HybridMessageSerializer(mockLogger.Object, serializers, mockDefaultSerializer.Object);
            var _ = target.Serialize(typeof(SampleOne), new SampleOne(), It.IsAny<IMessageContext>());

            // assert
            mockSerializer1.Verify(x => x.Serialize(typeof(SampleOne), It.IsAny<SampleOne>(), It.IsAny<IMessageContext>()));
            mockSerializer2.VerifyNoOtherCalls();
            mockDefaultSerializer.VerifyNoOtherCalls();
        }

        [Fact]
        public void When_AGenericMessageIsSerialized_Then_UseDefaultSerializer()
        {
            // arrange
            var mockDefaultSerializer = new Mock<IMessageSerializer>();
            mockDefaultSerializer.Setup(x => x.Serialize(typeof(SampleOne), It.IsAny<SampleOne>(), It.IsAny<IMessageContext>())).Verifiable(Times.Once());

            var mockSerializer1 = new Mock<IMessageSerializer>();

            var mockLogger = new Mock<ILogger<HybridMessageSerializer>>();
            var serializers = new Dictionary<IMessageSerializer, Type[]>
            {
                { mockSerializer1.Object, new[] { typeof(SampleTwo) } }
            };

            // act
            var target = new HybridMessageSerializer(mockLogger.Object, serializers, mockDefaultSerializer.Object);
            var _ = target.Serialize(typeof(SampleOne), new SampleOne(), It.IsAny<IMessageContext>());

            // assert
            mockDefaultSerializer.Verify(x => x.Serialize(typeof(SampleOne), It.IsAny<SampleOne>(), It.IsAny<IMessageContext>()));
            mockSerializer1.VerifyNoOtherCalls();
        }
    }
}

