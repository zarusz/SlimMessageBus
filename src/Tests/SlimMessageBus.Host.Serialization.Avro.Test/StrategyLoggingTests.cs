namespace SlimMessageBus.Host.Serialization.Avro.Test;

using Microsoft.Extensions.Logging;

using Moq;

public class StrategyLoggingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DictionaryStrategy_MissingRegistration_LogsMessageType(bool schemaLookup)
    {
        var logger = new Mock<ILogger<DictionarySchemaLookupStrategy>>();
        var factoryLogger = new Mock<ILogger<DictionaryMessageCreationStategy>>();
        logger.Setup(x => x.IsEnabled(LogLevel.Error)).Returns(true);
        factoryLogger.Setup(x => x.IsEnabled(LogLevel.Error)).Returns(true);
        Action act = schemaLookup
            ? () => new DictionarySchemaLookupStrategy(logger.Object).Lookup(typeof(string))
            : () => new DictionaryMessageCreationStategy(factoryLogger.Object).Create(typeof(string));

        act.Should().Throw<InvalidOperationException>().WithMessage("The type System.String does not have a * registered. Check your configuration.");

        var invocations = schemaLookup ? logger.Invocations : factoryLogger.Invocations;
        var log = invocations.Single(x => x.Method.Name == nameof(ILogger.Log));
        var state = (IEnumerable<KeyValuePair<string, object>>)log.Arguments[2];
        state.Should().Contain(x => x.Key == "MessageType" && Equals(x.Value, typeof(string)));
        state.Should().Contain(x => x.Key == "{OriginalFormat}" && ((string)x.Value).Contains("{MessageType}"));
    }

    [Fact]
    public void ReflectionSchemaStrategy_MissingField_LogsTypeAndField()
    {
        var logger = new Mock<ILogger<ReflectionSchemaLookupStrategy>>();
        logger.Setup(x => x.IsEnabled(LogLevel.Error)).Returns(true);
        var strategy = new ReflectionSchemaLookupStrategy(logger.Object, "CustomSchema");

        Action act = () => strategy.Lookup(typeof(string));

        act.Should().Throw<InvalidOperationException>().WithMessage("*CustomSchema*");
        var log = logger.Invocations.Single(x => x.Method.Name == nameof(ILogger.Log));
        var state = (IEnumerable<KeyValuePair<string, object>>)log.Arguments[2];
        state.Should().Contain(x => x.Key == "MessageType" && Equals(x.Value, typeof(string)));
        state.Should().Contain(x => x.Key == "FieldName" && Equals(x.Value, "CustomSchema"));
    }
}
