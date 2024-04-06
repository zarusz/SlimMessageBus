namespace SlimMessageBus.Host.Serialization.Avro.Test;

using SlimMessageBus.Host.Builders;

public class MessageBusBuilderExtensionsTests
{
    [Fact]
    public void When_AddAvroSerializer_Given_Builder_Then_ServicesRegistered()
    {
        // arrange
        var services = new ServiceCollection();
        var builder = MessageBusBuilder.Create();

        // act
        builder.AddAvroSerializer();

        // assert
        builder.PostConfigurationActions.ToList().ForEach(action => action(services));

        services.Should().ContainSingle(x => x.ServiceType == typeof(IMessageSerializer));
        services.Should().ContainSingle(x => x.ServiceType == typeof(AvroMessageSerializer));
    }

    [Fact]
    public void When_AddAvroSerializer_Given_HybridBuilder_Then_ServicesRegistered()
    {
        // arrange
        Action<IServiceCollection> registration = null;

        var services = new ServiceCollection();
        var mockHybridSerializationBuilder = new Mock<IHybridSerializationBuilder>();

        mockHybridSerializationBuilder.Setup(x => x.RegisterSerializer<AvroMessageSerializer>(It.IsAny<Action<IServiceCollection>>()))
            .Callback<Action<IServiceCollection>>(x => registration = x);

        // act
        MessageBusBuilderExtensions.AddAvroSerializer(mockHybridSerializationBuilder.Object);
        registration?.Invoke(services);

        // assert
        mockHybridSerializationBuilder.Verify(x => x.RegisterSerializer<AvroMessageSerializer>(It.IsAny<Action<IServiceCollection>>()), Times.Once);
        services.Should().ContainSingle(x => x.ServiceType == typeof(AvroMessageSerializer));
    }
}
