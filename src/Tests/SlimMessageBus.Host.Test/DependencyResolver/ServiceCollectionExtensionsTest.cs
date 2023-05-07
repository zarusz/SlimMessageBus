namespace SlimMessageBus.Host.Test.DependencyResolver;

using SlimMessageBus.Host.Test;
using SlimMessageBus.Host.Test.Messages;

public class ServiceCollectionExtensionsTest
{
    private readonly ServiceCollection _services;

    public ServiceCollectionExtensionsTest()
    {
        _services = new ServiceCollection();
    }

    [Fact]
    public void When_AddSlimMessageBus_Given_ModularizedSettingsWhichAddTheSameChildBus_Then_ChildBusSettingIsContinued()
    {
        // arrange
        var mockBus = new Mock<IMessageBus>();

        _services.AddSlimMessageBus(mbb =>
        {
            mbb.WithProvider(mbb => mockBus.Object);
            mbb.AddChildBus("bus1", mbb =>
            {
            });
        });

        _services.AddSlimMessageBus(mbb =>
        {
            mbb.AddChildBus("bus1", mbb =>
            {
            });
        });

        // act
        var serviceProvider = _services.BuildServiceProvider();

        // assert
        var mbb = serviceProvider.GetService<MessageBusBuilder>();

        mbb.Should().NotBeNull();
        var settings = mbb.Settings;
        settings.Should().NotBeNull();

        mbb.Children.Should().HaveCount(1);
        mbb.Children.Should().Contain(x => x.Key == "bus1");

        var childBus1Settings = mbb.Children["bus1"].Settings;
        childBus1Settings.Should().NotBeNull();
    }

    [Fact]
    public void When_AddSlimMessageBus_Given_ModularizedSettings_Then_AllOfTheModulesAreConcatenated()
    {
        // arrange
        var mockBus = new Mock<IMessageBus>();

        _services.AddSlimMessageBus();

        _services.AddSlimMessageBus(mbb =>
        {
            mbb.Produce<SomeMessage>(x => x.DefaultTopic("topic"));
            mbb.WithProvider(mbb => mockBus.Object);
            mbb.AddChildBus("bus1", mbb =>
            {
                mbb.Produce<SomeMessage2>(x => x.DefaultTopic("bus1_topic"));
            });
        });

        _services.AddSlimMessageBus(mbb =>
        {
            mbb.Consume<SomeMessage>(x => x.Topic("topic"));
            mbb.AddChildBus("bus1", mbb =>
            {
                mbb.Consume<SomeMessage2>(x => x.Topic("bus1_topic"));
            });
        });

        // act
        var serviceProvider = _services.BuildServiceProvider();

        // assert
        var mbb = serviceProvider.GetService<MessageBusBuilder>();

        mbb.Should().NotBeNull();
        var settings = mbb.Settings;
        settings.Should().NotBeNull();

        settings.Producers.Should().HaveCount(1);
        settings.Producers.Should().Contain(x => x.MessageType == typeof(SomeMessage) && x.PathKind == PathKind.Topic && x.DefaultPath == "topic");

        settings.Consumers.Should().HaveCount(1);
        settings.Consumers.Should().Contain(x => x.MessageType == typeof(SomeMessage) && x.PathKind == PathKind.Topic && x.Path == "topic");

        mbb.Children.Should().HaveCount(1);
        var childBus1Settings = mbb.Children["bus1"]?.Settings;
        childBus1Settings.Should().NotBeNull();

        childBus1Settings.Producers.Should().HaveCount(1);
        childBus1Settings.Producers.Should().Contain(x => x.MessageType == typeof(SomeMessage2) && x.PathKind == PathKind.Topic && x.DefaultPath == "bus1_topic");

        childBus1Settings.Consumers.Should().HaveCount(1);
        childBus1Settings.Consumers.Should().Contain(x => x.MessageType == typeof(SomeMessage2) && x.PathKind == PathKind.Topic && x.Path == "bus1_topic");
    }

    [Fact]
    public void When_AddSlimMessageBus_Given_AddServicesFromAssembly_Then_AllOfTheConsumersAreRegisteredInMSDI()
    {
        // arrange
        var mockBus = new Mock<IMessageBus>();

        _services.AddSlimMessageBus();

        // act
        _services.AddSlimMessageBus(mbb =>
        {
            mbb.AddChildBus("bus1", mbb =>
            {
                mbb.AddServicesFromAssemblyContaining<ServiceCollectionExtensionsTest>(x => x.Name == nameof(SomeMessageConsumer), consumerLifetime: ServiceLifetime.Scoped);
            });
        });

        _services.AddSlimMessageBus(mbb =>
        {
            mbb.AddChildBus("bus2", mbb =>
            {
                mbb.AddServicesFromAssemblyContaining<ServiceCollectionExtensionsTest>(x => x.Name == nameof(SomeMessageConsumer), consumerLifetime: ServiceLifetime.Transient);
            });
        });

        // assert
        _services.Count(x => x.ServiceType == typeof(SomeMessageConsumer)).Should().Be(1);
        _services.Count(x => x.ServiceType == typeof(IConsumer<SomeMessage>)).Should().Be(1);
        _services.Should().Contain(x => x.ServiceType == typeof(SomeMessageConsumer) && x.ImplementationType == typeof(SomeMessageConsumer) && x.Lifetime == ServiceLifetime.Scoped);
        _services.Should().Contain(x => x.ServiceType == typeof(IConsumer<SomeMessage>) && x.ImplementationFactory != null && x.Lifetime == ServiceLifetime.Scoped);
    }
}
