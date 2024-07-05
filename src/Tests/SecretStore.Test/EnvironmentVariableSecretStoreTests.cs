namespace SecretStore.Test;

public class EnvironmentVariableSecretStoreTests
{
    [Fact]
    public void Given_EnvWithEmptyToken_When_GetSecret_Then_ReturnsEmptyString()
    {
        // Arrange
        Environment.SetEnvironmentVariable("kafka_username", "(empty)");
        Environment.SetEnvironmentVariable("mqtt_username", "R2D2");

        var secretStore = new EnvironmentVariableSecretStore();

        // Actr
        var kafkaUsername = secretStore.GetSecret("kafka_username");
        var mqttUsername = secretStore.GetSecret("mqtt_username");

        // Assert
        kafkaUsername.Should().Be(string.Empty);
        mqttUsername.Should().Be("R2D2");
    }
}