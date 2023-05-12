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

    [Fact]
    public void When_GetOrDefault_Given_ValueInParentNotInCurrent_Then_ReturnsParentValue()
    {
        // arrange
        _bus.Properties["key"] = "parent-value";

        // act
        var result = _producerSettings.GetOrDefault("key", _bus as HasProviderExtensions, "default");

        // assert

        result.Should().Be("parent-value");
    }

    [Fact]
    public void When_GetOrDefault_Given_ValueNotPresent_Then_ReturnsDefault()
    {
        // arrange

        // act
        var result = _producerSettings.GetOrDefault("key", (HasProviderExtensions)null, "default");

        // assert

        result.Should().Be("default");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_GetOrCreate_Given_ValueNotPresent_Then_CreatesValuesAndStores(bool valueExisted)
    {
        // arrange
        var value = "value1";
        var key = "key1";
        if (valueExisted)
        {
            _producerSettings.Properties[key] = value;
        }
        var factoryMethodMock = new Mock<Func<string>>();
        factoryMethodMock.Setup(x => x()).Returns(value);

        // act
        var result = _producerSettings.GetOrCreate(key, factoryMethodMock.Object);

        // assert
        result.Should().Be(value);
        _producerSettings.Properties[key].Should().Be(value);
        factoryMethodMock.Verify(x => x(), Times.Exactly(valueExisted ? 0 : 1));
        factoryMethodMock.VerifyNoOtherCalls();
    }
}