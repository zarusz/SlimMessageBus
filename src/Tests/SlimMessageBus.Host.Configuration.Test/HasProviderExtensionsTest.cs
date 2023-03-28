namespace SlimMessageBus.Host.Test.Config;

public class HasProviderExtensionsTest
{
    private readonly string _key;
    private readonly ProducerSettings _producerSettings;
    private readonly MessageBusSettings _bus;
    private readonly MessageBusSettings _childBus;

    public HasProviderExtensionsTest()
    {
        _key = "flag1";
        _producerSettings = new ProducerSettings();
        _bus = new MessageBusSettings();
        _childBus = new MessageBusSettings(_bus);
    }

    [Fact]
    public void When_GetOrDefault_GivenValueSetAtTheProducer_ThenReturnsThatValue()
    {
        // arrange
        _producerSettings.Properties.Add(_key, true);
        _childBus.Properties.Add(_key, false);
        _bus.Properties.Add(_key, false);

        // act
        var result = _producerSettings.GetOrDefault(_key, _childBus, false);

        // assert
        result.Should().BeTrue();
    }

    [Fact]
    public void When_GetOrDefault_GivenValueSetAtTheImmediateBus_ThenReturnsThatValue()
    {
        // arrange
        _childBus.Properties.Add(_key, true);
        _bus.Properties.Add(_key, false);

        // act
        var result = _producerSettings.GetOrDefault(_key, _childBus, false);

        // assert
        result.Should().BeTrue();
    }

    [Fact]
    public void When_GetOrDefault_GivenValueSetAtTheMostParentBus_ThenReturnsThatValue()
    {
        // arrange
        _bus.Properties.Add(_key, true);

        // act
        var result = _producerSettings.GetOrDefault(_key, _childBus, false);

        // assert
        result.Should().BeTrue();
    }

    [Fact]
    public void When_GetOrDefault_GivenValueNotSetAnywhere_ThenReturnsDefaultValue()
    {
        // arrange

        // act
        var result = _producerSettings.GetOrDefault(_key, _childBus, false);

        // assert
        result.Should().BeFalse();
    }
}