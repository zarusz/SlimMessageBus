namespace SlimMessageBus.Host.Serialization.SystemTextJson.Test;

using Moq;

public class JsonMessageSerializerTests
{
    public static TheoryData<object, object> Data => new()
    {
        { null, null},
        { 10, 10},
        { false, false},
        { true, true},
        { "string", "string"},
        { DateTime.Now.Date, DateTime.Now.Date},
        { Guid.Empty, "00000000-0000-0000-0000-000000000000"},
    };

    [Theory]
    [MemberData(nameof(Data))]
    public void When_SerializeAndDeserialize_Given_TypeObjectAndBytesPayload_Then_TriesToInferPrimitiveTypes(object value, object expectedValue)
    {
        // arrange
        var subject = new JsonMessageSerializer();

        // act
        var bytes = subject.Serialize(typeof(object), value, It.IsAny<IMessageContext>());
        var deserializedValue = subject.Deserialize(typeof(object), bytes, It.IsAny<IMessageContext>());

        // assert
        deserializedValue.Should().Be(expectedValue);
    }

    [Theory]
    [MemberData(nameof(Data))]
    public void When_SerializeAndDeserialize_Given_TypeObjectAndStringPayload_Then_TriesToInferPrimitiveTypes(object value, object expectedValue)
    {
        // arrange
        var subject = new JsonMessageSerializer() as IMessageSerializer<string>;

        // act
        var json = subject.Serialize(typeof(object), value, It.IsAny<IMessageContext>());
        var deserializedValue = subject.Deserialize(typeof(object), json, It.IsAny<IMessageContext>());

        // assert
        deserializedValue.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_RegisterSerializer_Then_UsesOptionsFromContainerIfAvailable(bool jsonOptionComeFromContainer)
    {
        // arrange
        var mbb = MessageBusBuilder.Create();
        mbb.AddJsonSerializer();

        var services = new ServiceCollection();

        var jsonOptions = new JsonSerializerOptions();

        // Simulate the options have been used in an serializer already (see https://github.com/zarusz/SlimMessageBus/issues/252)
        // Modifying options (adding converters) when the options are already in use by a serializer will throw an exception: System.InvalidOperationException : This JsonSerializerOptions instance is read-only or has already been used in serialization or deserialization.
        JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>(), jsonOptions);

        if (jsonOptionComeFromContainer)
        {
            services.AddSingleton(jsonOptions);
        }

        foreach (var action in mbb.PostConfigurationActions)
        {
            action(services);
        }

        var serviceProvider = services.BuildServiceProvider();

        var subject = serviceProvider.GetRequiredService<JsonMessageSerializer>();

        // act
        if (jsonOptionComeFromContainer)
        {
            subject.Options.Should().BeSameAs(jsonOptions);
        }
        else
        {
            subject.Options.Should().NotBeSameAs(jsonOptions);
        }

        // assert
        serviceProvider.GetService<IMessageSerializer>().Should().BeSameAs(subject);
    }
}
