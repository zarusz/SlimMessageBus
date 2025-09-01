namespace SlimMessageBus.Host.AzureServiceBus.Test.Config;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

public class AsbConsumerBuilderExtensionsTests
{
    private class TestConsumerBuilder(MessageBusSettings settings, Type messageType, string path = null)
        : AbstractConsumerBuilder(settings, messageType, path)
    {
    }

    private class TestMessage { }

    private readonly MessageBusSettings _settings;
    private readonly TestConsumerBuilder _consumerBuilder;

    public AsbConsumerBuilderExtensionsTests()
    {
        _settings = new MessageBusSettings();
        _consumerBuilder = new TestConsumerBuilder(_settings, typeof(TestMessage));
    }

    #region Queue Tests

    [Fact]
    public void When_Queue_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => AsbConsumerBuilderExtensions.Queue<TestConsumerBuilder>(null, "test-queue");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void When_Queue_Given_NullQueueName_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => _consumerBuilder.Queue(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("queue");
    }

    [Fact]
    public void When_Queue_Given_ValidParameters_Then_SetsPathAndPathKind()
    {
        // Arrange
        var queueName = "test-queue";

        // Act
        var result = _consumerBuilder.Queue(queueName);

        // Assert
        result.Should().BeSameAs(_consumerBuilder);
        _consumerBuilder.ConsumerSettings.Path.Should().Be(queueName);
        _consumerBuilder.ConsumerSettings.PathKind.Should().Be(PathKind.Queue);
    }

    [Fact]
    public void When_Queue_Given_ConfigAction_Then_InvokesAction()
    {
        // Arrange
        var queueName = "test-queue";
        var configActionInvoked = false;

        Action<TestConsumerBuilder> configAction = builder =>
        {
            configActionInvoked = true;
            builder.Should().BeSameAs(_consumerBuilder);
        };

        // Act
        _consumerBuilder.Queue(queueName, configAction);

        // Assert
        configActionInvoked.Should().BeTrue();
    }

    #endregion

    #region SubscriptionName Tests

    [Fact]
    public void When_SubscriptionName_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => AsbConsumerBuilderExtensions.SubscriptionName<TestConsumerBuilder>(null, "test-subscription");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void When_SubscriptionName_Given_NullSubscriptionName_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => _consumerBuilder.SubscriptionName(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("subscriptionName");
    }

    [Fact]
    public void When_SubscriptionName_Given_QueuePathKind_Then_ThrowsConfigurationMessageBusException()
    {
        // Arrange
        _consumerBuilder.ConsumerSettings.Path = "test-queue";
        _consumerBuilder.ConsumerSettings.PathKind = PathKind.Queue;

        // Act
        var action = () => _consumerBuilder.SubscriptionName("test-subscription");

        // Assert
        action.Should().Throw<ConfigurationMessageBusException>()
            .WithMessage("*subscription name configuration*does not apply to Azure ServiceBus queues*");
    }

    [Fact]
    public void When_SubscriptionName_Given_TopicPathKind_Then_SetsSubscriptionName()
    {
        // Arrange
        var subscriptionName = "test-subscription";
        _consumerBuilder.ConsumerSettings.Path = "test-topic";
        _consumerBuilder.ConsumerSettings.PathKind = PathKind.Topic;

        // Act
        var result = _consumerBuilder.SubscriptionName(subscriptionName);

        // Assert
        result.Should().BeSameAs(_consumerBuilder);
        _consumerBuilder.ConsumerSettings.GetOrDefault<string>(AsbProperties.SubscriptionNameKey).Should().Be(subscriptionName);
    }

    #endregion

    #region MaxAutoLockRenewalDuration Tests

    [Fact]
    public void When_MaxAutoLockRenewalDuration_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => AsbConsumerBuilderExtensions.MaxAutoLockRenewalDuration<TestConsumerBuilder>(null, TimeSpan.FromMinutes(5));

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void When_MaxAutoLockRenewalDuration_Given_ValidDuration_Then_SetsDuration()
    {
        // Arrange
        var duration = TimeSpan.FromMinutes(5);

        // Act
        var result = _consumerBuilder.MaxAutoLockRenewalDuration(duration);

        // Assert
        result.Should().BeSameAs(_consumerBuilder);
        _consumerBuilder.ConsumerSettings.GetOrDefault<TimeSpan?>(AsbProperties.MaxAutoLockRenewalDurationKey).Should().Be(duration);
    }

    #endregion

    #region SubQueue Tests

    [Fact]
    public void When_SubQueue_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => AsbConsumerBuilderExtensions.SubQueue<TestConsumerBuilder>(null, SubQueue.DeadLetter);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void When_SubQueue_Given_ValidSubQueue_Then_SetsSubQueue()
    {
        // Arrange
        var subQueue = SubQueue.DeadLetter;

        // Act
        var result = _consumerBuilder.SubQueue(subQueue);

        // Assert
        result.Should().BeSameAs(_consumerBuilder);
        _consumerBuilder.ConsumerSettings.GetOrDefault<SubQueue?>(AsbProperties.SubQueueKey).Should().Be(subQueue);
    }

    #endregion

    #region PrefetchCount Tests

    [Fact]
    public void When_PrefetchCount_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => AsbConsumerBuilderExtensions.PrefetchCount<TestConsumerBuilder>(null, 100);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void When_PrefetchCount_Given_ValidCount_Then_SetsPrefetchCount()
    {
        // Arrange
        var prefetchCount = 100;

        // Act
        var result = _consumerBuilder.PrefetchCount(prefetchCount);

        // Assert
        result.Should().BeSameAs(_consumerBuilder);
        _consumerBuilder.ConsumerSettings.GetOrDefault<int?>(AsbProperties.PrefetchCountKey).Should().Be(prefetchCount);
    }

    #endregion

    #region EnableSession Tests

    [Fact]
    public void When_EnableSession_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => AsbConsumerBuilderExtensions.EnableSession<TestConsumerBuilder>(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void When_EnableSession_Given_NoConfiguration_Then_SetsEnableSession()
    {
        // Act
        var result = _consumerBuilder.EnableSession();

        // Assert
        result.Should().BeSameAs(_consumerBuilder);
        _consumerBuilder.ConsumerSettings.GetOrDefault(AsbProperties.EnableSessionKey, false).Should().BeTrue();
    }

    [Fact]
    public void When_EnableSession_Given_SessionConfiguration_Then_AppliesConfiguration()
    {
        // Arrange
        var sessionIdleTimeout = TimeSpan.FromMinutes(10);
        var maxConcurrentSessions = 20;

        // Act
        var result = _consumerBuilder.EnableSession(session =>
        {
            session.SessionIdleTimeout(sessionIdleTimeout);
            session.MaxConcurrentSessions(maxConcurrentSessions);
        });

        // Assert
        result.Should().BeSameAs(_consumerBuilder);
        _consumerBuilder.ConsumerSettings.GetOrDefault(AsbProperties.EnableSessionKey, false).Should().BeTrue();
        _consumerBuilder.ConsumerSettings.GetOrDefault<TimeSpan?>(AsbProperties.SessionIdleTimeoutKey).Should().Be(sessionIdleTimeout);
        _consumerBuilder.ConsumerSettings.GetOrDefault<int?>(AsbProperties.MaxConcurrentSessionsKey).Should().Be(maxConcurrentSessions);
    }

    #endregion

    #region SubscriptionSqlFilter Tests

    [Fact]
    public void When_SubscriptionSqlFilter_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => AsbConsumerBuilderExtensions.SubscriptionSqlFilter<TestConsumerBuilder>(null, "priority > 5");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void When_SubscriptionSqlFilter_Given_ValidParameters_Then_AddsRule()
    {
        // Arrange
        var filterSql = "priority > 5";
        var ruleName = "high-priority";
        var actionSql = "SET priority = priority + 10";

        // Act
        var result = _consumerBuilder.SubscriptionSqlFilter(filterSql, ruleName, actionSql);

        // Assert
        result.Should().BeSameAs(_consumerBuilder);

        var rules = _consumerBuilder.ConsumerSettings.GetOrDefault<IDictionary<string, SubscriptionRule>>(AsbProperties.RulesKey);
        rules.Should().NotBeNull();
        rules.Should().ContainKey(ruleName);

        var rule = rules[ruleName] as SubscriptionSqlRule;
        rule.Should().NotBeNull();
        rule.Name.Should().Be(ruleName);
        rule.SqlFilter.Should().Be(filterSql);
        rule.SqlAction.Should().Be(actionSql);
    }

    [Fact]
    public void When_SubscriptionSqlFilter_Given_DefaultRuleName_Then_UsesDefault()
    {
        // Arrange
        var filterSql = "priority > 5";

        // Act
        _consumerBuilder.SubscriptionSqlFilter(filterSql);

        // Assert
        var rules = _consumerBuilder.ConsumerSettings.GetOrDefault<IDictionary<string, SubscriptionRule>>(AsbProperties.RulesKey);
        rules.Should().ContainKey("default");
    }

    #endregion

    #region SubscriptionCorrelationFilter Tests

    [Fact]
    public void When_SubscriptionCorrelationFilter_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => AsbConsumerBuilderExtensions.SubscriptionCorrelationFilter<TestConsumerBuilder>(null, correlationId: "correlationId");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void When_SubscriptionCorrelationFilter_Given_NoFilterValues_Then_ThrowsArgumentException()
    {
        // Act
        var action = () => _consumerBuilder.SubscriptionCorrelationFilter();

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("At least one property must contain a value to use as a filter");
    }

    [Fact]
    public void When_SubscriptionCorrelationFilter_Given_ValidParameters_Then_AddsRule()
    {
        // Arrange
        var ruleName = "correlation-rule";
        var correlationId = "my-correlation";
        var messageId = "message-123";
        var applicationProperties = new Dictionary<string, object>
        {
            { "CustomProperty", "Value" }
        };

        // Act
        var result = _consumerBuilder.SubscriptionCorrelationFilter(
            ruleName: ruleName,
            correlationId: correlationId,
            messageId: messageId,
            applicationProperties: applicationProperties);

        // Assert
        result.Should().BeSameAs(_consumerBuilder);

        var rules = _consumerBuilder.ConsumerSettings.GetOrDefault<IDictionary<string, SubscriptionRule>>(AsbProperties.RulesKey);
        rules.Should().NotBeNull();
        rules.Should().ContainKey(ruleName);

        var rule = rules[ruleName] as SubscriptionCorrelationRule;
        rule.Should().NotBeNull();
        rule.Name.Should().Be(ruleName);
        rule.CorrelationId.Should().Be(correlationId);
        rule.MessageId.Should().Be(messageId);
        rule.ApplicationProperties.Should().BeSameAs(applicationProperties);
    }

    #endregion

    #region CreateQueueOptions Tests

    [Fact]
    public void When_CreateQueueOptions_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => AsbConsumerBuilderExtensions.CreateQueueOptions<TestConsumerBuilder>(null, _ => { });

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void When_CreateQueueOptions_Given_NullAction_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => _consumerBuilder.CreateQueueOptions(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("action");
    }

    [Fact]
    public void When_CreateQueueOptions_Given_ValidAction_Then_SetsOptions()
    {
        // Arrange
        Action<CreateQueueOptions> configAction = options => options.AutoDeleteOnIdle = TimeSpan.FromHours(1);

        // Act
        var result = _consumerBuilder.CreateQueueOptions(configAction);

        // Assert
        result.Should().BeSameAs(_consumerBuilder);

        // Verify that the action is stored, we can't access the stored action directly
        _consumerBuilder.ConsumerSettings.GetOrDefault(AsbProperties.CreateQueueOptions).Should().BeSameAs(configAction);
    }

    #endregion

    #region CreateTopicOptions Tests

    [Fact]
    public void When_CreateTopicOptions_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => AsbConsumerBuilderExtensions.CreateTopicOptions<TestConsumerBuilder>(null, _ => { });

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void When_CreateTopicOptions_Given_NullAction_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => _consumerBuilder.CreateTopicOptions(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("action");
    }

    [Fact]
    public void When_CreateTopicOptions_Given_ValidAction_Then_SetsOptions()
    {
        // Arrange
        Action<CreateTopicOptions> configAction = options => options.EnablePartitioning = true;

        // Act
        var result = _consumerBuilder.CreateTopicOptions(configAction);

        // Assert
        result.Should().BeSameAs(_consumerBuilder);

        // Verify that the action is stored, we can't access the stored action directly
        _consumerBuilder.ConsumerSettings.GetOrDefault(AsbProperties.CreateTopicOptions).Should().BeSameAs(configAction);
    }

    #endregion

    #region CreateSubscriptionOptions Tests

    [Fact]
    public void When_CreateSubscriptionOptions_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => AsbConsumerBuilderExtensions.CreateSubscriptionOptions<TestConsumerBuilder>(null, _ => { });

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void When_CreateSubscriptionOptions_Given_NullAction_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => _consumerBuilder.CreateSubscriptionOptions(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("action");
    }

    [Fact]
    public void When_CreateSubscriptionOptions_Given_ValidAction_Then_SetsOptions()
    {
        // Arrange
        Action<CreateSubscriptionOptions> configAction = options => options.RequiresSession = true;

        // Act
        var result = _consumerBuilder.CreateSubscriptionOptions(configAction);

        // Assert
        result.Should().BeSameAs(_consumerBuilder);

        // Verify that the action is stored, we can't access the stored action directly
        _consumerBuilder.ConsumerSettings.GetOrDefault(AsbProperties.CreateSubscriptionOptions).Should().BeSameAs(configAction);
    }

    #endregion
}
