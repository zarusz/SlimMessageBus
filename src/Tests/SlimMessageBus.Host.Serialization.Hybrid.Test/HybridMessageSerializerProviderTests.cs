namespace SlimMessageBus.Host.Serialization.Hybrid.Test;

public class HybridMessageSerializerProviderTests
{
    [Fact]
    public void When_ConstructorReceivesARepeatedDefinition_Then_ThrowException()
    {
        // arrange
        var mockDefaultSerializer = new Mock<IMessageSerializerProvider>();
        var mockSerializer1 = new Mock<IMessageSerializerProvider>();
        var mockSerializer2 = new Mock<IMessageSerializerProvider>();
        var mockLogger = new Mock<ILogger<HybridMessageSerializerProvider>>();

        var serializers = new Dictionary<IMessageSerializerProvider, Type[]>
        {
            { mockSerializer1.Object, new[] { typeof(SampleOne), typeof(SampleTwo) } },
            { mockSerializer2.Object, new[] { typeof(SampleOne) } },
        };

        // act
        var act = () => new HybridMessageSerializerProvider(mockLogger.Object, serializers, mockDefaultSerializer.Object);

        // assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void When_NoDefaultSerializerIsSupplied_Then_UseFirstSpecialist()
    {
        // arrange
        var mockDefaultSerializer = new Mock<IMessageSerializerProvider>();
        var mockSerializer1 = new Mock<IMessageSerializerProvider>();
        var mockLogger = new Mock<ILogger<HybridMessageSerializerProvider>>();

        var serializers = new Dictionary<IMessageSerializerProvider, Type[]>
        {
            { mockDefaultSerializer.Object, new[] { typeof(SampleOne) } },
            { mockSerializer1.Object, new[] { typeof(SampleTwo) } },
        };

        // act
        var target = new HybridMessageSerializerProvider(mockLogger.Object, serializers, default);
        var actual = target.DefaultSerializer;

        // assert
        actual.Should().BeEquivalentTo(mockDefaultSerializer.Object);
    }

    [Fact]
    public void When_ASpecialisedMessageIsSerialized_Then_UseSpecializedSerializer()
    {
        // arrange
        var mockDefaultSerializer = new Mock<IMessageSerializerProvider>();

        var mockSerializer1 = new Mock<IMessageSerializer>();
        mockSerializer1.Setup(x => x.Serialize(typeof(SampleOne), It.IsAny<SampleOne>())).Verifiable(Times.Once());

        var mockSerializerProvider1 = new Mock<IMessageSerializerProvider>();
        mockSerializerProvider1.Setup(x => x.GetSerializer(It.IsAny<string>())).Returns(mockSerializer1.Object);

        var mockSerializerProvider2 = new Mock<IMessageSerializerProvider>();

        var mockLogger = new Mock<ILogger<HybridMessageSerializerProvider>>();
        var serializers = new Dictionary<IMessageSerializerProvider, Type[]>
        {
            { mockSerializerProvider1.Object, new[] { typeof(SampleOne) } },
            { mockSerializerProvider2.Object, new[] { typeof(SampleTwo) } },
        };

        var target = new HybridMessageSerializerProvider(mockLogger.Object, serializers, mockDefaultSerializer.Object);

        // act
        var _ = target.GetSerializer("").Serialize(typeof(SampleOne), new SampleOne());

        // assert
        mockSerializer1.Verify(x => x.Serialize(typeof(SampleOne), It.IsAny<SampleOne>()));
        mockSerializer1.VerifyNoOtherCalls();

        mockSerializerProvider1.Verify(x => x.GetSerializer(It.IsAny<string>()));
        mockSerializerProvider1.VerifyNoOtherCalls();

        mockSerializerProvider2.VerifyNoOtherCalls();

        mockDefaultSerializer.VerifyNoOtherCalls();
    }

    [Fact]
    public void When_AGenericMessageIsSerialized_Then_UseDefaultSerializer()
    {
        // arrange
        var mockDefaultSerializer = new Mock<IMessageSerializer>();
        mockDefaultSerializer.Setup(x => x.Serialize(typeof(SampleOne), It.IsAny<SampleOne>())).Verifiable(Times.Once());

        var mockDefaultSerializerProvider = new Mock<IMessageSerializerProvider>();
        mockDefaultSerializerProvider.Setup(x => x.GetSerializer(It.IsAny<string>())).Returns(mockDefaultSerializer.Object);

        var mockSerializerProvider1 = new Mock<IMessageSerializerProvider>();

        var mockLogger = new Mock<ILogger<HybridMessageSerializerProvider>>();
        var serializers = new Dictionary<IMessageSerializerProvider, Type[]>
        {
            { mockSerializerProvider1.Object, new[] { typeof(SampleTwo) } }
        };

        var target = new HybridMessageSerializerProvider(mockLogger.Object, serializers, mockDefaultSerializerProvider.Object);

        // act
        var _ = target.GetSerializer("").Serialize(typeof(SampleOne), new SampleOne());

        // assert
        mockDefaultSerializer.Verify(x => x.Serialize(typeof(SampleOne), It.IsAny<SampleOne>()));
        mockDefaultSerializer.VerifyNoOtherCalls();

        mockDefaultSerializerProvider.Verify(x => x.GetSerializer(""));
        mockDefaultSerializerProvider.VerifyNoOtherCalls();

        mockSerializerProvider1.VerifyNoOtherCalls();
    }
}

