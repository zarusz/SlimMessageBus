namespace SlimMessageBus.Host.Serialization.Hybrid.Test;

using Microsoft.Extensions.DependencyInjection;

public class MessageBusBuilderExtensionsTests
{
    private readonly IServiceCollection _services;

    public MessageBusBuilderExtensionsTests()
    {
        // arrange
        var mockLogger = new Mock<ILogger<HybridMessageSerializer>>();

        _services = new ServiceCollection();
        _services.AddSingleton(mockLogger.Object);

        // act
        _services.AddSlimMessageBus(cfg =>
        {
            cfg.AddHybridSerializer(builder =>
            {
                builder
                    .RegisterSerializer<SerializerOne>(services => services.AddSingleton<SerializerOne>())
                    .AsDefault();

                builder
                    .RegisterSerializer<SerializerTwo>(services => services.AddSingleton<SerializerTwo>())
                    .For(typeof(SampleTwo));

                builder
                    .RegisterSerializer<SerializerThree>(services => services.AddSingleton<SerializerThree>())
                    .For(typeof(SampleThree));
            });
        });
    }

    [Fact]
    public void When_IMessageSerializerRegistrationsAlreadyExist_Then_RemovePreviousRegistrations()
    {
        // assert
        _services.Count(x => x.ServiceType == typeof(IMessageSerializer)).Should().Be(1);
    }

    [Fact]
    public void When_HybridMessageSerializerIsAdded_Then_RegisterAsIMessageSerializer()
    {
        // act
        var serviceProvider = _services.BuildServiceProvider();
        var target = serviceProvider.GetServices<IMessageSerializer>().ToList();

        // assert
        target.Count.Should().Be(1);
        target.Single().GetType().Should().Be(typeof(HybridMessageSerializer));
    }

    [Fact]
    public void When_HybridMessageSerializerIsAdded_Then_SerializersAndTypesShouldConfigured()
    {
        // act
        var serviceProvider = _services.BuildServiceProvider();
        var target = serviceProvider.GetService<HybridMessageSerializer>();

        // assert
        target.DefaultSerializer.GetType().Should().Be(typeof(SerializerOne));
        target.SerializerByType.Count.Should().Be(2);
        target.SerializerByType.Should().ContainKey(typeof(SampleTwo)).WhoseValue.Should().BeOfType<SerializerTwo>();
        target.SerializerByType.Should().ContainKey(typeof(SampleThree)).WhoseValue.Should().BeOfType<SerializerThree>();
    }

    public abstract class AbstractSerializer : IMessageSerializer
    {
        public object Deserialize(Type t, byte[] payload)
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize(Type t, object message)
        {
            throw new NotImplementedException();
        }
    }

    public class SerializerOne : AbstractSerializer { }
    public class SerializerTwo : AbstractSerializer { }
    public class SerializerThree : AbstractSerializer { }
}