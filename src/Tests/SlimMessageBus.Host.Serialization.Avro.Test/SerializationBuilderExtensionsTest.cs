namespace SlimMessageBus.Host.Serialization.Avro.Test;

public class SerializationBuilderExtensionsTest
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

        services.Should().ContainSingle(x => x.ServiceType == typeof(IMessageSerializerProvider));
        services.Should().ContainSingle(x => x.ServiceType == typeof(AvroMessageSerializer));
    }
}
