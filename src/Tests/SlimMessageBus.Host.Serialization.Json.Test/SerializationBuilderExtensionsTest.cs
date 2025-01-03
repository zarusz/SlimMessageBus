namespace SlimMessageBus.Host.Serialization.Json.Test;

public class SerializationBuilderExtensionsTest
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
        services.Should().ContainSingle(x => x.ServiceType == typeof(IMessageSerializer<string>));
        services.Should().ContainSingle(x => x.ServiceType == typeof(JsonMessageSerializer));
    }
}
