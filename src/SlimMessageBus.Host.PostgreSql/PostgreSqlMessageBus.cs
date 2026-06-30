namespace SlimMessageBus.Host.PostgreSql;

public class PostgreSqlMessageBus : MessageBusBase<PostgreSqlMessageBusSettings>
{
    private readonly KindMapping _kindMapping = new();
    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public PostgreSqlMessageBus(MessageBusSettings settings, PostgreSqlMessageBusSettings providerSettings)
        : base(settings, providerSettings)
    {
        OnBuildProvider();
    }

    protected override IMessageBusSettingsValidationService ValidationService => new PostgreSqlMessageBusSettingsValidationService(Settings, ProviderSettings);

    protected override void Build()
    {
        base.Build();

        _kindMapping.Configure(Settings);
        InitTaskList.Add(ProvisionTopology, CancellationToken);
    }

    public override async Task ProvisionTopology()
    {
        await base.ProvisionTopology().ConfigureAwait(false);

        var scope = Settings.ServiceProvider.CreateScope();
        try
        {
            var repository = (PostgreSqlRepository)scope.ServiceProvider.GetRequiredService<IPostgreSqlRepository>();
            var template = scope.ServiceProvider.GetRequiredService<PostgreSqlTemplate>();
            var topologyService = new PostgreSqlTopologyService(LoggerFactory.CreateLogger<PostgreSqlTopologyService>(), ProviderSettings, repository, template);

            await topologyService.Migrate(CancellationToken).ConfigureAwait(false);

            foreach (var consumer in Settings.Consumers.Where(x => x.PathKind == PathKind.Topic))
            {
                await repository.UpsertSubscription(consumer.Path, consumer.GetSubscriptionName(), CancellationToken).ConfigureAwait(false);
            }

            if (Settings.RequestResponse?.PathKind == PathKind.Topic)
            {
                await repository.UpsertSubscription(Settings.RequestResponse.Path, Settings.RequestResponse.GetSubscriptionName(), CancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await scope.DisposeAsyncScope().ConfigureAwait(false);
        }
    }

    protected override async Task CreateConsumers()
    {
        await base.CreateConsumers().ConfigureAwait(false);

        MessageProvider<PostgreSqlTransportMessage> GetMessageProvider(string path)
            => SerializerProvider.GetSerializer(path).GetMessageProvider<byte[], PostgreSqlTransportMessage>(t => t.MessagePayload);

        foreach (var ((path, pathKind, subscriptionName), consumerSettings) in Settings.Consumers
            .GroupBy(x => (x.Path, x.PathKind, SubscriptionName: x.PathKind == PathKind.Topic ? x.GetSubscriptionName() : null))
            .ToDictionary(x => x.Key, x => x.ToList()))
        {
            var processor = new MessageProcessor<PostgreSqlTransportMessage>(
                consumerSettings,
                messageBus: this,
                messageProvider: GetMessageProvider(path),
                path: path,
                responseProducer: this,
                consumerErrorHandlerOpenGenericType: typeof(IPostgreSqlConsumerErrorHandler<>));

            AddPostgreSqlConsumer(consumerSettings, processor, path, pathKind, subscriptionName);
        }

        if (Settings.RequestResponse != null)
        {
            var path = Settings.RequestResponse.Path;
            var pathKind = Settings.RequestResponse.PathKind;
            var subscriptionName = pathKind == PathKind.Topic ? Settings.RequestResponse.GetSubscriptionName() : null;
            var processor = new ResponseMessageProcessor<PostgreSqlTransportMessage>(LoggerFactory, Settings.RequestResponse, GetMessageProvider(path), PendingRequestStore, TimeProvider);

            AddPostgreSqlConsumer([Settings.RequestResponse], processor, path, pathKind, subscriptionName);
        }
    }

    private void AddPostgreSqlConsumer(IEnumerable<AbstractConsumerSettings> consumerSettings, IMessageProcessor<PostgreSqlTransportMessage> processor, string path, PathKind pathKind, string? subscriptionName)
    {
        var instances = consumerSettings.Max(x => x.Instances);
        for (var i = 0; i < instances; i++)
        {
            var consumer = new PostgreSqlConsumer(
                LoggerFactory.CreateLogger<PostgreSqlConsumer>(),
                consumerSettings,
                Settings.ServiceProvider.GetServices<IAbstractConsumerInterceptor>(),
                Settings.ServiceProvider,
                ProviderSettings,
                processor,
                path,
                pathKind,
                subscriptionName,
                $"{_instanceId}-{path}-{subscriptionName}-{i}");
            AddConsumer(consumer);
        }
    }

    public override async Task ProduceToTransport(object message, Type messageType, string path, IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        try
        {
            OnProduceToTransport(message, messageType, path, messageHeaders);

            var serviceProvider = targetBus?.ServiceProvider ?? Settings.ServiceProvider;
            var scope = serviceProvider.CreateScope();
            try
            {
                var repository = scope.ServiceProvider.GetRequiredService<IPostgreSqlRepository>();
                var kind = _kindMapping.GetKind(messageType, path);
                var payload = SerializerProvider.GetSerializer(path).Serialize(messageType, messageHeaders, message, null);
                var messageTypeName = MessageTypeResolver.ToName(messageType);

                if (kind == PathKind.Topic)
                {
                    var subscriptions = await repository.GetSubscriptions(path, cancellationToken).ConfigureAwait(false);
                    foreach (var subscriptionName in subscriptions)
                    {
                        await repository.Insert(CreateTransportMessage(serviceProvider, path, kind, subscriptionName, messageTypeName, payload, messageHeaders), cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    await repository.Insert(CreateTransportMessage(serviceProvider, path, kind, null, messageTypeName, payload, messageHeaders), cancellationToken).ConfigureAwait(false);
                }

                if (repository is PostgreSqlRepository postgreSqlRepository)
                {
                    await postgreSqlRepository.Notify(path, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                await scope.DisposeAsyncScope().ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not ProducerMessageBusException && ex is not TaskCanceledException)
        {
            throw new ProducerMessageBusException(GetProducerErrorMessage(path, message, messageType, ex), ex);
        }
    }

    public override async Task<ProduceToTransportBulkResult<T>> ProduceToTransportBulk<T>(IReadOnlyCollection<T> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        var dispatched = new List<T>();
        try
        {
            foreach (var envelope in envelopes)
            {
                await ProduceToTransport(envelope.Message, envelope.MessageType, path, envelope.Headers, targetBus, cancellationToken).ConfigureAwait(false);
                dispatched.Add(envelope);
            }
            return new ProduceToTransportBulkResult<T>(dispatched, null);
        }
        catch (Exception ex)
        {
            return new ProduceToTransportBulkResult<T>(dispatched, ex);
        }
    }

    private PostgreSqlTransportMessage CreateTransportMessage(IServiceProvider serviceProvider, string path, PathKind pathKind, string? subscriptionName, string messageType, byte[] payload, IDictionary<string, object> headers)
    {
        var guidGenerator = ProviderSettings.IdGeneration.GuidGenerator ?? (IGuidGenerator)serviceProvider.GetRequiredService(ProviderSettings.IdGeneration.GuidGeneratorType);
        return new PostgreSqlTransportMessage
        {
            Id = ProviderSettings.IdGeneration.Mode == PostgreSqlMessageIdGenerationMode.ClientGuidGenerator ? guidGenerator.NewGuid() : Guid.Empty,
            Path = path,
            PathKind = pathKind,
            SubscriptionName = subscriptionName,
            MessageType = messageType,
            MessagePayload = payload,
            Headers = headers != null ? new Dictionary<string, object>(headers) : null
        };
    }
}
