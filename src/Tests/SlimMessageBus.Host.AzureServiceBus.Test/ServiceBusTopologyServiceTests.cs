namespace SlimMessageBus.Host.AzureServiceBus.Test
{
    using Azure;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;

    using Microsoft.Extensions.Logging;

    public static class ServiceBusTopologyServiceTests
    {
        public class ProvisionTopologyTests
        {
            private const string _topicName = "test-topic";

            const string _subscriptionName = "test-subscription";

            private readonly Mock<ServiceBusAdministrationClient> _mockAdminClient;

            private readonly Mock<ILogger<ServiceBusTopologyService>> _mockLogger;

            private readonly ServiceBusTopologyService _target;

            private readonly ConsumerBuilder<SampleMessage> _defaultConsumerBuilder;

            public ProvisionTopologyTests()
            {
                _mockAdminClient = new Mock<ServiceBusAdministrationClient>();

                MessageBusSettings = new MessageBusSettings();

                ProviderBusSettings = new ServiceBusMessageBusSettings("connection-string")
                {
                    AdminClientFactory = (_, _) => _mockAdminClient.Object,
                    TopologyProvisioning = new ServiceBusTopologySettings
                    {
                        Enabled = false
                    }
                };

                _defaultConsumerBuilder = new ConsumerBuilder<SampleMessage>(MessageBusSettings)
                    .Topic(_topicName)
                    .SubscriptionName(_subscriptionName)
                    .WithConsumer<SampleConsumer<SampleMessage>>();

                _mockLogger = new Mock<ILogger<ServiceBusTopologyService>>();

                _target = new ServiceBusTopologyService(_mockLogger.Object, MessageBusSettings, ProviderBusSettings);
            }

            private ServiceBusMessageBusSettings ProviderBusSettings { get; }
            private MessageBusSettings MessageBusSettings { get; }

            [Fact]
            public async Task When_TopicDoesNotExistAndCanCreateTopic_Then_CreateTopic()
            {
                // arrange
                ProviderBusSettings.TopologyProvisioning.CanConsumerCreateTopic = true;

                CreateTopicOptions createTopicOptions = null;

                _mockAdminClient.Setup(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>())).Returns(ResponseTask(false));
                _mockAdminClient.Setup(x => x.CreateTopicAsync(It.IsAny<CreateTopicOptions>(), It.IsAny<CancellationToken>())).Callback<CreateTopicOptions, CancellationToken>((options, _) => createTopicOptions = options);

                // act
                await _target.ProvisionTopology();

                // assert
                _mockAdminClient.Verify(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>()), Times.Once);
                _mockAdminClient.Verify(x => x.CreateTopicAsync(It.IsAny<CreateTopicOptions>(), It.IsAny<CancellationToken>()), Times.Once);

                createTopicOptions.Should().NotBeNull();
                createTopicOptions.Name.Should().Be(_topicName);
            }

            [Fact]
            public async Task When_TopicDoesNotExistButCannotCreateTopic_Then_DoNotCreateTopic()
            {
                // arrange
                ProviderBusSettings.TopologyProvisioning.CanConsumerCreateTopic = false;

                _mockAdminClient.Setup(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>())).Returns(ResponseTask(false));
                _mockAdminClient.Setup(x => x.CreateTopicAsync(It.IsAny<CreateTopicOptions>(), It.IsAny<CancellationToken>())).Verifiable();

                // act
                await _target.ProvisionTopology();

                // assert
                _mockAdminClient.Verify(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>()), Times.Once);
                _mockAdminClient.Verify(x => x.CreateTopicAsync(It.IsAny<CreateTopicOptions>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            [Fact]
            public async Task When_SubscriptionDoesNotExistAndCanCreateSubscription_Then_CreateSubscription()
            {
                // arrange
                ProviderBusSettings.TopologyProvisioning.CanConsumerCreateSubscription = true;

                CreateSubscriptionOptions createSubscriptionOptions = null;

                _mockAdminClient.Setup(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.SubscriptionExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(ResponseTask(false));
                _mockAdminClient.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOptions>(), It.IsAny<CancellationToken>())).Callback<CreateSubscriptionOptions, CancellationToken>((options, _) => createSubscriptionOptions = options);

                // act
                await _target.ProvisionTopology();

                // assert
                _mockAdminClient.Verify(x => x.SubscriptionExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
                _mockAdminClient.Verify(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOptions>(), It.IsAny<CancellationToken>()), Times.Once);

                createSubscriptionOptions.Should().NotBeNull();
                createSubscriptionOptions.SubscriptionName.Should().Be(_subscriptionName);
            }

            [Fact]
            public async Task When_SubscriptionDoesNotExistButCannotCreateSubscription_Then_DoNotCreateSubscription()
            {
                // arrange
                ProviderBusSettings.TopologyProvisioning.CanConsumerCreateSubscription = false;

                _mockAdminClient.Setup(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.SubscriptionExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(ResponseTask(false));
                _mockAdminClient.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOptions>(), It.IsAny<CancellationToken>())).Verifiable();

                // act
                await _target.ProvisionTopology();

                // assert
                _mockAdminClient.Verify(x => x.SubscriptionExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
                _mockAdminClient.Verify(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOptions>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            [Fact]
            public async Task When_SubscriptionIsSharedBetweenConsumers_But_ConfigurationDiffers_Then_DoNotCreateSubscription()
            {
                const string subscriptionName = "test-subscription";

                // arrange
                new ConsumerBuilder<SampleMessage>(MessageBusSettings)
                    .Topic(_topicName)
                    .SubscriptionName(subscriptionName)
                    .EnableSession();

                ProviderBusSettings.TopologyProvisioning.CanConsumerCreateSubscription = true;

                _mockAdminClient.Setup(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.SubscriptionExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(ResponseTask(false));
                _mockAdminClient.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOptions>(), It.IsAny<CancellationToken>()));

                // act
                await _target.ProvisionTopology();

                // assert
                _mockAdminClient.Verify(x => x.SubscriptionExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
                _mockAdminClient.Verify(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOptions>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            [Fact]
            public async Task When_SubscriptionIsCreated_AndCanCreateSubscriptionFilter_AndNoOtherRulesDeclared_Then_NeverDeleteDefaultRule()
            {
                // arrange
                ProviderBusSettings.TopologyProvisioning.CanConsumerCreateSubscription = true;
                ProviderBusSettings.TopologyProvisioning.CanConsumerCreateSubscriptionFilter = true;

                _mockAdminClient.Setup(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.SubscriptionExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(ResponseTask(false));
                _mockAdminClient.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOptions>(), It.IsAny<CancellationToken>()));

                // act
                await _target.ProvisionTopology();

                // assert
                _mockAdminClient.Verify(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOptions>(), It.IsAny<CancellationToken>()), Times.Once);
                _mockAdminClient.Verify(x => x.DeleteRuleAsync(_topicName, _subscriptionName, RuleProperties.DefaultRuleName, It.IsAny<CancellationToken>()), Times.Never);
            }

            [Fact]
            public async Task When_SubscriptionIsCreated_AndCannotCreateSubscriptionFilter_Then_DoNotDeleteDefaultRule()
            {
                // arrange
                ProviderBusSettings.TopologyProvisioning.CanConsumerCreateSubscription = true;
                ProviderBusSettings.TopologyProvisioning.CanConsumerCreateSubscriptionFilter = false;

                _mockAdminClient.Setup(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.SubscriptionExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(ResponseTask(false));
                _mockAdminClient.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOptions>(), It.IsAny<CancellationToken>()));
                _mockAdminClient.Setup(x => x.DeleteRuleAsync(_topicName, _subscriptionName, RuleProperties.DefaultRuleName, It.IsAny<CancellationToken>())).Verifiable();

                // act
                await _target.ProvisionTopology();

                // assert
                _mockAdminClient.Verify(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOptions>(), It.IsAny<CancellationToken>()), Times.Once);
                _mockAdminClient.Verify(x => x.DeleteRuleAsync(_topicName, _subscriptionName, RuleProperties.DefaultRuleName, It.IsAny<CancellationToken>()), Times.Never);
            }

            [Fact]
            public async Task When_FiltersAreMissing_And_CanCreateSubscriptionFilters_Then_CreateRule()
            {
                // arrange
                const string ruleName = "rule-name";

                _defaultConsumerBuilder
                    .SubscriptionSqlFilter("1 = 1", ruleName);

                ProviderBusSettings.TopologyProvisioning.CanConsumerCreateSubscriptionFilter = true;

                CreateRuleOptions createRuleOptions = null;

                _mockAdminClient.Setup(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.SubscriptionExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.GetRulesAsync(_topicName, _subscriptionName, It.IsAny<CancellationToken>())).Returns(AsyncPage<RuleProperties>());
                _mockAdminClient.Setup(x => x.CreateRuleAsync(_topicName, _subscriptionName, It.IsAny<CreateRuleOptions>(), It.IsAny<CancellationToken>())).Callback<string, string, CreateRuleOptions, CancellationToken>((_, _, options, _) => createRuleOptions = options);

                // act
                await _target.ProvisionTopology();

                // assert
                _mockAdminClient.Verify(x => x.CreateRuleAsync(_topicName, _subscriptionName, It.IsAny<CreateRuleOptions>(), It.IsAny<CancellationToken>()), Times.Once);

                createRuleOptions.Should().NotBeNull();
                createRuleOptions.Name.Should().Be(ruleName);
            }

            [Fact]
            public async Task When_FiltersAreMissing_And_CannotCreateSubscriptionFilters_Then_DoNotCreateRule()
            {
                // arrange
                const string ruleName = "rule-name";

                _defaultConsumerBuilder
                    .SubscriptionSqlFilter("1 = 1", ruleName);

                ProviderBusSettings.TopologyProvisioning.CanConsumerCreateSubscriptionFilter = false;

                _mockAdminClient.Setup(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.SubscriptionExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.GetRulesAsync(_topicName, _subscriptionName, It.IsAny<CancellationToken>())).Returns(AsyncPage<RuleProperties>());
                _mockAdminClient.Setup(x => x.CreateRuleAsync(_topicName, _subscriptionName, It.IsAny<CreateRuleOptions>(), It.IsAny<CancellationToken>())).Verifiable();

                // act
                await _target.ProvisionTopology();

                // assert
                _mockAdminClient.Verify(x => x.CreateRuleAsync(_topicName, _subscriptionName, It.IsAny<CreateRuleOptions>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            [Fact]
            public async Task When_FilterExistsOnServerOnly_And_CanReplaceSubscriptionFilters_Then_DeleteRule()
            {
                // arrange
                const string ruleName = "rule-name";

                ProviderBusSettings.TopologyProvisioning.CanConsumerReplaceSubscriptionFilters = true;

                _mockAdminClient.Setup(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.SubscriptionExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.GetRulesAsync(_topicName, _subscriptionName, It.IsAny<CancellationToken>())).Returns(AsyncPage(ServiceBusModelFactory.RuleProperties(ruleName, new SqlRuleFilter("1 = 1"))));
                _mockAdminClient.Setup(x => x.DeleteRuleAsync(_topicName, _subscriptionName, ruleName, It.IsAny<CancellationToken>())).Verifiable();

                // act
                await _target.ProvisionTopology();

                // assert
                _mockAdminClient.Verify(x => x.DeleteRuleAsync(_topicName, _subscriptionName, ruleName, It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task When_FilterExistsOnServerOnly_And_CannotReplaceSubscriptionFilters_Then_DoNotDeleteRule()
            {
                // arrange
                const string ruleName = "rule-name";

                ProviderBusSettings.TopologyProvisioning.CanConsumerReplaceSubscriptionFilters = false;

                _mockAdminClient.Setup(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.SubscriptionExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.GetRulesAsync(_topicName, _subscriptionName, It.IsAny<CancellationToken>())).Returns(AsyncPage(ServiceBusModelFactory.RuleProperties(ruleName, new SqlRuleFilter("1 = 1"))));
                _mockAdminClient.Setup(x => x.DeleteRuleAsync(_topicName, _subscriptionName, ruleName, It.IsAny<CancellationToken>())).Verifiable();

                // act
                await _target.ProvisionTopology();

                // assert
                _mockAdminClient.Verify(x => x.DeleteRuleAsync(_topicName, _subscriptionName, ruleName, It.IsAny<CancellationToken>()), Times.Never);
            }

            [Fact]
            public async Task When_FilterConfigurationDiffersWithServer_And_CanReplaceSubscriptionFilters_Then_UpdateRule()
            {
                // arrange
                const string ruleName = "rule-name";

                const string ruleInFilter = "1 = 1";
                const string ruleOnServer = "1 = 2";

                _defaultConsumerBuilder
                    .SubscriptionSqlFilter(ruleInFilter, ruleName);

                ProviderBusSettings.TopologyProvisioning.CanConsumerReplaceSubscriptionFilters = true;

                RuleProperties ruleProperties = null;

                _mockAdminClient.Setup(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.SubscriptionExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.GetRulesAsync(_topicName, _subscriptionName, It.IsAny<CancellationToken>())).Returns(AsyncPage(ServiceBusModelFactory.RuleProperties(ruleName, new SqlRuleFilter(ruleOnServer))));
                _mockAdminClient.Setup(x => x.UpdateRuleAsync(_topicName, _subscriptionName, It.IsAny<RuleProperties>(), It.IsAny<CancellationToken>())).Callback<string, string, RuleProperties, CancellationToken>((_, _, rule, _) => ruleProperties = rule);

                // act
                await _target.ProvisionTopology();

                // assert
                _mockAdminClient.Verify(x => x.UpdateRuleAsync(_topicName, _subscriptionName, It.IsAny<RuleProperties>(), It.IsAny<CancellationToken>()), Times.Once);

                ruleProperties.Should().NotBeNull();
                ruleProperties.Name.Should().Be(ruleName);
                ruleProperties.Filter.Should().BeOfType<SqlRuleFilter>();
                ((SqlRuleFilter)ruleProperties.Filter).SqlExpression.Should().Be(ruleInFilter);
            }

            [Fact]
            public async Task When_FilterConfigurationDiffersWithServer_And_CannotReplaceSubscriptionFilters_Then_DoNotUpdateRule()
            {
                // arrange
                const string ruleName = "rule-name";

                const string ruleInFilter = "1 = 1";
                const string ruleOnServer = "1 = 2";

                _defaultConsumerBuilder
                    .SubscriptionSqlFilter(ruleInFilter, ruleName);

                ProviderBusSettings.TopologyProvisioning.CanConsumerReplaceSubscriptionFilters = false;

                _mockAdminClient.Setup(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.SubscriptionExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.GetRulesAsync(_topicName, _subscriptionName, It.IsAny<CancellationToken>())).Returns(AsyncPage(ServiceBusModelFactory.RuleProperties(ruleName, new SqlRuleFilter(ruleOnServer))));
                _mockAdminClient.Setup(x => x.UpdateRuleAsync(_topicName, _subscriptionName, It.IsAny<RuleProperties>(), It.IsAny<CancellationToken>())).Verifiable();

                // act
                await _target.ProvisionTopology();

                // assert
                _mockAdminClient.Verify(x => x.UpdateRuleAsync(_topicName, _subscriptionName, It.IsAny<RuleProperties>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            [Fact]
            public async Task When_FilterTypeConfigurationDiffersWithServer_And_CannotReplaceSubscriptionFilters_Then_DoNotUpdateRule()
            {
                // arrange
                const string ruleName = "rule-name";

                var applicationProperties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Sample", "Value"}
                };

                _defaultConsumerBuilder
                    .SubscriptionCorrelationFilter(ruleName, applicationProperties: applicationProperties);

                ProviderBusSettings.TopologyProvisioning.CanConsumerReplaceSubscriptionFilters = false;

                _mockAdminClient.Setup(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.SubscriptionExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.GetRulesAsync(_topicName, _subscriptionName, It.IsAny<CancellationToken>())).Returns(AsyncPage(ServiceBusModelFactory.RuleProperties(ruleName, new SqlRuleFilter("1 = 1"))));
                _mockAdminClient.Setup(x => x.UpdateRuleAsync(_topicName, _subscriptionName, It.IsAny<RuleProperties>(), It.IsAny<CancellationToken>())).Verifiable();

                // act
                await _target.ProvisionTopology();

                // assert
                _mockAdminClient.Verify(x => x.UpdateRuleAsync(_topicName, _subscriptionName, It.IsAny<RuleProperties>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            [Fact]
            public async Task When_FilterConfigurationDiffersWithServer_And_CanValidateSubscriptionFiltersOnly_Then_DoNotChangeAnyRules()
            {
                // arrange
                const string ruleToDelete = "ToDelete";
                const string ruleToInsert = "ToInsert";
                const string ruleToUpdate = "ToUpdate";

                const string filter = "1 = 1";

                _defaultConsumerBuilder
                    .SubscriptionSqlFilter(filter, ruleToInsert)
                    .SubscriptionSqlFilter(filter, ruleToUpdate);

                var serverRules = new[]
                {
                    ServiceBusModelFactory.RuleProperties(ruleToDelete, new SqlRuleFilter(filter), new SqlRuleAction("SET MessageType = 'Test'")),
                    ServiceBusModelFactory.RuleProperties(ruleToUpdate, new SqlRuleFilter(filter), new SqlRuleAction("SET MessageType = 'Test'"))
                };

                ProviderBusSettings.TopologyProvisioning.CanConsumerCreateSubscriptionFilter = false;
                ProviderBusSettings.TopologyProvisioning.CanConsumerReplaceSubscriptionFilters = false;
                ProviderBusSettings.TopologyProvisioning.CanConsumerValidateSubscriptionFilters = true;

                _mockAdminClient.Setup(x => x.TopicExistsAsync(_topicName, It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.SubscriptionExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(ResponseTask(true));
                _mockAdminClient.Setup(x => x.GetRulesAsync(_topicName, _subscriptionName, It.IsAny<CancellationToken>())).Returns(AsyncPage(serverRules));
                _mockAdminClient.Setup(x => x.CreateRuleAsync(_topicName, _subscriptionName, It.IsAny<CreateRuleOptions>(), It.IsAny<CancellationToken>())).Verifiable();
                _mockAdminClient.Setup(x => x.UpdateRuleAsync(_topicName, _subscriptionName, It.IsAny<RuleProperties>(), It.IsAny<CancellationToken>())).Verifiable();
                _mockAdminClient.Setup(x => x.DeleteRuleAsync(_topicName, _subscriptionName, It.IsAny<string>(), It.IsAny<CancellationToken>())).Verifiable();

                // act
                await _target.ProvisionTopology();

                // assert
                _mockAdminClient.Verify(x => x.GetRulesAsync(_topicName, _subscriptionName, It.IsAny<CancellationToken>()), Times.Once);
                _mockAdminClient.Verify(x => x.CreateRuleAsync(_topicName, _subscriptionName, It.IsAny<CreateRuleOptions>(), It.IsAny<CancellationToken>()), Times.Never);
                _mockAdminClient.Verify(x => x.UpdateRuleAsync(_topicName, _subscriptionName, It.IsAny<RuleProperties>(), It.IsAny<CancellationToken>()), Times.Never);
                _mockAdminClient.Verify(x => x.DeleteRuleAsync(_topicName, _subscriptionName, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            [Fact]
            public void When_SubscriptionCorrelationFilter_ContainsNoConfiguration_ThrowException()
            {
                // act
                var act = () => _defaultConsumerBuilder
                    .SubscriptionCorrelationFilter("rule-name");

                // assert
                act.Should().Throw<ArgumentException>();
            }
        }

        private static Task<Response<T>> ResponseTask<T>(T value)
        {
            var mockResponse = new Mock<Response<T>>();
            mockResponse.Setup(x => x.Value).Returns(value);

            return Task.FromResult(mockResponse.Object);
        }

        public static AsyncPageable<T> AsyncPage<T>(params T[] items)
        {
            var mockResponse = new Mock<Response>();

            var pages = new[] {
                Page<T>.FromValues(items, null, mockResponse.Object)
            };

            return AsyncPageable<T>.FromPages(pages);
        }

        private record SampleMessage
        {
        }

        private class SampleConsumer<T> : IConsumer<T>
        {
            public Task OnHandle(T message, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
