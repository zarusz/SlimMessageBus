namespace SlimMessageBus.Host.Sql.Test;

public class SqlMessageBusSettingsValidationServiceTests
{
    private readonly SqlMessageBusSettings _providerSettings = new()
    {
        ConnectionString = "Server=localhost;Database=smb;User Id=sa;Password=Password1!;TrustServerCertificate=True"
    };

    private readonly SqlMessageBusSettingsValidationService _subject;

    public SqlMessageBusSettingsValidationServiceTests()
    {
        var settings = new MessageBusSettings
        {
            Name = "TestBus",
            ServiceProvider = new ServiceCollection().BuildServiceProvider()
        };
        _subject = new SqlMessageBusSettingsValidationService(settings, _providerSettings);
    }

    [Fact]
    public void AssertSettings_GivenValidSettings_ThenDoesNotThrow()
    {
        var act = _subject.AssertSettings;

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AssertSettings_GivenConnectionStringIsMissing_ThenThrows(string connectionString)
    {
        _providerSettings.ConnectionString = connectionString;

        var act = _subject.AssertSettings;

        act.Should().Throw<ConfigurationMessageBusException>()
           .WithMessage("*connection string*");
    }

    [Fact]
    public void AssertSettings_GivenPollBatchSizeIsInvalid_ThenThrows()
    {
        _providerSettings.PollBatchSize = 0;

        var act = _subject.AssertSettings;

        act.Should().Throw<ConfigurationMessageBusException>()
           .WithMessage("*poll batch size*");
    }

    [Fact]
    public void AssertSettings_GivenMaxDeliveryAttemptsIsInvalid_ThenThrows()
    {
        _providerSettings.MaxDeliveryAttempts = 0;

        var act = _subject.AssertSettings;

        act.Should().Throw<ConfigurationMessageBusException>()
           .WithMessage("*max delivery attempts*");
    }
}
