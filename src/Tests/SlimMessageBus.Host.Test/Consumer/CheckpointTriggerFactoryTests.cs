namespace SlimMessageBus.Host.Test.Consumer;

public class CheckpointTriggerFactoryTests
{
    private readonly Mock<Func<IReadOnlyCollection<CheckpointValue>, string>> _checkpointMessageFactoryMock = new();
    private readonly CheckpointTriggerFactory _subject;

    public CheckpointTriggerFactoryTests()
    {
        _subject = new CheckpointTriggerFactory(NullLoggerFactory.Instance, _checkpointMessageFactoryMock.Object);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetValueFromSettings_Then_ValuePropertySet(bool allValuesSame)
    {
        // arrange
        var count = 3;
        var duration = TimeSpan.FromSeconds(2);

        var collection = Enumerable.Range(0, 2)
            .Select(i =>
            {
                var consumerSettings = new ConsumerSettings();
                consumerSettings.Properties[CheckpointSettings.CheckpointCount] = allValuesSame ? count : i;
                consumerSettings.Properties[CheckpointSettings.CheckpointDuration] = duration;
                return consumerSettings;
            })
            .ToList();

        // act        
        var act = () => _subject.Create(collection);

        // assert
        if (allValuesSame)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<ConfigurationMessageBusException>();
        }
    }
}