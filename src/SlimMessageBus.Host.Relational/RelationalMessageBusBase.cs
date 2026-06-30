namespace SlimMessageBus.Host.Relational;

using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Consumer.ErrorHandling;
using SlimMessageBus.Host.Interceptor;

public abstract class RelationalMessageBusBase<TSettings, TMessage, TRepository> : MessageBusBase<TSettings>
    where TSettings : class, IRelationalMessageBusSettings
    where TMessage : IRelationalTransportMessage
    where TRepository : IRelationalRepository<TMessage>
{
    private readonly KindMapping _kindMapping = new();
    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    protected RelationalMessageBusBase(MessageBusSettings settings, TSettings providerSettings)
        : base(settings, providerSettings)
    {
        OnBuildProvider();
    }

    protected abstract Type ConsumerErrorHandlerOpenGenericType { get; }

    protected override void Build()
    {
        base.Build();

        _kindMapping.Configure(Settings);
        InitTaskList.Add(ProvisionTopology, CancellationToken);
    }

    protected async Task ProvisionSubscriptions(TRepository repository)
    {
        foreach (var consumer in Settings.Consumers.Where(x => x.PathKind == PathKind.Topic))
        {
            await repository.UpsertSubscription(consumer.Path, GetSubscriptionName(consumer), CancellationToken).ConfigureAwait(false);
        }

        if (Settings.RequestResponse?.PathKind == PathKind.Topic)
        {
            await repository.UpsertSubscription(Settings.RequestResponse.Path, GetSubscriptionName(Settings.RequestResponse), CancellationToken).ConfigureAwait(false);
        }
    }

    protected override async Task CreateConsumers()
    {
        await base.CreateConsumers().ConfigureAwait(false);

        MessageProvider<TMessage> GetMessageProvider(string path)
            => SerializerProvider.GetSerializer(path).GetMessageProvider<byte[], TMessage>(t => t.MessagePayload);

        foreach (var ((path, pathKind, subscriptionName), consumerSettings) in Settings.Consumers
            .GroupBy(x => (x.Path, x.PathKind, SubscriptionName: x.PathKind == PathKind.Topic ? GetSubscriptionName(x) : null))
            .ToDictionary(x => x.Key, x => x.ToList()))
        {
            var processor = new MessageProcessor<TMessage>(
                consumerSettings,
                messageBus: this,
                messageProvider: GetMessageProvider(path),
                path: path,
                responseProducer: this,
                consumerErrorHandlerOpenGenericType: ConsumerErrorHandlerOpenGenericType);

            AddRelationalConsumers(consumerSettings, processor, path, pathKind, subscriptionName);
        }

        if (Settings.RequestResponse != null)
        {
            var path = Settings.RequestResponse.Path;
            var pathKind = Settings.RequestResponse.PathKind;
            var subscriptionName = pathKind == PathKind.Topic ? GetSubscriptionName(Settings.RequestResponse) : null;
            var processor = new ResponseMessageProcessor<TMessage>(LoggerFactory, Settings.RequestResponse, GetMessageProvider(path), PendingRequestStore, TimeProvider);

            AddRelationalConsumers([Settings.RequestResponse], processor, path, pathKind, subscriptionName);
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
                var repository = scope.ServiceProvider.GetRequiredService<TRepository>();
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

                await AfterProduce(repository, path, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (scope is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    scope.Dispose();
                }
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

    private void AddRelationalConsumers(IEnumerable<AbstractConsumerSettings> consumerSettings, IMessageProcessor<TMessage> processor, string path, PathKind pathKind, string subscriptionName)
    {
        var instances = consumerSettings.Max(x => x.Instances);
        for (var i = 0; i < instances; i++)
        {
            AddConsumer(CreateConsumer(consumerSettings, processor, path, pathKind, subscriptionName, $"{_instanceId}-{path}-{subscriptionName}-{i}"));
        }
    }

    protected abstract string GetSubscriptionName(AbstractConsumerSettings settings);

    protected abstract AbstractConsumer CreateConsumer(IEnumerable<AbstractConsumerSettings> consumerSettings, IMessageProcessor<TMessage> processor, string path, PathKind pathKind, string subscriptionName, string instanceId);

    protected abstract TMessage CreateTransportMessage(IServiceProvider serviceProvider, string path, PathKind pathKind, string subscriptionName, string messageType, byte[] payload, IDictionary<string, object> headers);

    protected virtual Task AfterProduce(TRepository repository, string path, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
