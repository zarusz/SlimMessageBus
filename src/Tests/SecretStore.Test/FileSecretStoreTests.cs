namespace SecretStore.Test;

public class FileSecretStoreTests
{
    [Fact]
    public void Given_TextFileWithKeyValuePairsSeparatedByEqualsSign_When_Constructor_Then_GetSecretMethodReturnsProperValue()
    {
        // Arrange
        var fileContents = "kafka_username=JohnDoe\nkafka_username=JohnWick\n#commented mqtt_username=123\n  mqtt_username=";
        var tempFilePath = Path.GetTempFileName();
        File.WriteAllText(tempFilePath, fileContents);
        try
        {
            var secretStore = new FileSecretStore(tempFilePath);

            // Act
            var kafkaUsername = secretStore.GetSecret("kafka_username");
            var mqttUsername = secretStore.GetSecret("mqtt_username");

            // Assert
            kafkaUsername.Should().Be("JohnWick");
            mqttUsername.Should().Be(string.Empty);

        }
        finally
        {
            // Clean up
            File.Delete(tempFilePath);
        }
    }
}
