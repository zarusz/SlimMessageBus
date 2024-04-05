namespace SlimMessageBus.Host.Serialization.GoogleProtobuf.Test;

public class SerializationBuilderExtensionsTest
{
    [Fact]
    public void When_AddGoogleProtobufSerializer_Given_Builder_Then_ServicesRegistered()
    {
        // arrange
        var services = new ServiceCollection();
        var builder = MessageBusBuilder.Create();

        // act
        builder.AddGoogleProtobufSerializer();

        // assert
        builder.PostConfigurationActions.ToList().ForEach(action => action(services));

        services.Should().ContainSingle(x => x.ServiceType == typeof(IMessageSerializer));
        services.Should().ContainSingle(x => x.ServiceType == typeof(GoogleProtobufMessageSerializer));
    }
}
