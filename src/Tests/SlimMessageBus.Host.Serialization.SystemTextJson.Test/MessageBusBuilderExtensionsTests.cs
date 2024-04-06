namespace SlimMessageBus.Host.Serialization.SystemTextJson.Test;

using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host.Builders;
using SlimMessageBus.Host.Serialization.SystemTextJson;

public class MessageBusBuilderExtensionsTests
{
    [Fact]
    public void When_AddJsonSerializer_Given_Builder_Then_ServicesRegistered()
    {
        // arrange
        var services = new ServiceCollection();
        var builder = MessageBusBuilder.Create();

        // act
        builder.AddJsonSerializer();

        // assert
        builder.PostConfigurationActions.ToList().ForEach(action => action(services));

        services.Should().ContainSingle(x => x.ServiceType == typeof(IMessageSerializer));
        services.Should().ContainSingle(x => x.ServiceType == typeof(JsonMessageSerializer));
    }

    [Fact]
    public void When_AddJsonSerializer_Given_HybridBuilder_Then_ServicesRegistered()
    {
        // arrange
        Action<IServiceCollection> registration = null;

        var services = new ServiceCollection();
        var mockHybridSerializationBuilder = new Mock<IHybridSerializationBuilder>();

        mockHybridSerializationBuilder.Setup(x => x.RegisterSerializer<JsonMessageSerializer>(It.IsAny<Action<IServiceCollection>>()))
            .Callback<Action<IServiceCollection>>(x => registration = x);

        // act
        MessageBusBuilderExtensions.AddJsonSerializer(mockHybridSerializationBuilder.Object, null);
        registration?.Invoke(services);

        // assert
        mockHybridSerializationBuilder.Verify(x => x.RegisterSerializer<JsonMessageSerializer>(It.IsAny<Action<IServiceCollection>>()), Times.Once);
        services.Should().ContainSingle(x => x.ServiceType == typeof(JsonMessageSerializer));
    }
}
