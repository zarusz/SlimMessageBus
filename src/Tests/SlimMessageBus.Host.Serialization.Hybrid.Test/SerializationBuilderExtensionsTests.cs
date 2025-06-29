namespace SlimMessageBus.Host.Serialization.Hybrid.Test;

using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;

public class SerializationBuilderExtensionsTests
{
    private readonly IServiceCollection _services;

    public SerializationBuilderExtensionsTests()
    {
        var mockLogger = new Mock<ILogger<HybridMessageSerializerProvider>>();

        _services = new ServiceCollection();
        _services.AddSingleton(mockLogger.Object);
    }

    [Fact]
    public void When_HybridMessageSerializerIsAdded_Then_RegisterAsIMessageSerializer()
    {
        // arrange
        _services.AddSlimMessageBus(cfg =>
        {
            cfg.AddHybridSerializer(builder =>
            {
                builder
                    .AsDefault()
                    .RegisterSerializer<SerializerOne>(services => services.AddSingleton<SerializerOne>());

                builder
                    .For(typeof(SampleTwo))
                    .RegisterSerializer<SerializerTwo>(services => services.AddSingleton<SerializerTwo>());

                builder
                    .For(typeof(SampleThree))
                    .RegisterSerializer<SerializerThree>(services => services.AddSingleton<SerializerThree>());
            });
        });

        var serviceProvider = _services.BuildServiceProvider();

        // act
        var target = serviceProvider.GetServices<IMessageSerializerProvider>().ToList();

        // assert
        target.Count.Should().Be(1);
        target.Single().GetType().Should().Be(typeof(HybridMessageSerializerProvider));
    }

    [Fact]
    public void When_HybridMessageSerializerIsAdded_Then_SerializersAndTypesShouldConfigured()
    {
        // arrange
        _services.AddSlimMessageBus(cfg =>
        {
            cfg.AddHybridSerializer(builder =>
            {
                builder
                    .AsDefault()
                    .RegisterSerializer<SerializerOne>(services => services.AddSingleton<SerializerOne>());

                builder
                    .For(typeof(SampleTwo))
                    .RegisterSerializer<SerializerTwo>(services => services.AddSingleton<SerializerTwo>());

                builder
                    .For(typeof(SampleThree))
                    .RegisterSerializer<SerializerThree>(services => services.AddSingleton<SerializerThree>());
            });
        });

        var serviceProvider = _services.BuildServiceProvider();

        // act
        var target = serviceProvider.GetService<HybridMessageSerializerProvider>();

        // assert
        target.DefaultSerializer.GetType().Should().Be(typeof(SerializerOne));
        target.SerializerByType.Count.Should().Be(2);
        target.SerializerByType.Should().ContainKey(typeof(SampleTwo)).WhoseValue.Should().BeOfType<SerializerTwo>();
        target.SerializerByType.Should().ContainKey(typeof(SampleThree)).WhoseValue.Should().BeOfType<SerializerThree>();
    }

    [Fact]
    public void When_IMessageSerializerRegistrationsAlreadyExist_Then_ThrowException()
    {
        // arrange
        _services.AddSlimMessageBus(cfg =>
        {
            cfg.RegisterSerializer<SerializerOne>(services => services.AddSingleton<SerializerOne>());

            cfg.AddHybridSerializer(builder =>
            {
                builder
                    .AsDefault()
                    .RegisterSerializer<SerializerTwo>(services => services.AddSingleton<SerializerTwo>());
            });
        });

        var serviceProvider = _services.BuildServiceProvider();

        // act
        var act = () => serviceProvider.GetService<HybridMessageSerializerProvider>();


        // arrange
        act.Should().Throw<NotSupportedException>();
    }

    public abstract class AbstractSerializer : IMessageSerializer, IMessageSerializerProvider
    {
        public byte[] Serialize(Type messageType, IDictionary<string, object> headers, object message, object transportMessage)
        {
            throw new NotImplementedException();
        }

        public object Deserialize(Type messageType, IReadOnlyDictionary<string, object> headers, byte[] payload, object transportMessage)
        {
            throw new NotImplementedException();
        }

        public IMessageSerializer GetSerializer(string path) => this;
    }

    public class SerializerOne : AbstractSerializer { }
    public class SerializerTwo : AbstractSerializer { }
    public class SerializerThree : AbstractSerializer { }
}