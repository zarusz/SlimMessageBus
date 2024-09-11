namespace SlimMessageBus.Host.Nats;

using Microsoft.Extensions.Primitives;

public class NatsMessageBus : MessageBusBase<NatsMessageBusSettings>
{
    private readonly ILogger _logger;
    private NatsConnection _connection;

    public NatsMessageBus(MessageBusSettings settings, NatsMessageBusSettings providerSettings) : base(settings, providerSettings)
    {
        _logger = LoggerFactory.CreateLogger<NatsMessageBus>();
        OnBuildProvider();
    }

    protected override IMessageBusSettingsValidationService ValidationService => new NatsMessageBusSettingsValidationService(Settings, ProviderSettings);

    public bool IsConnected => _connection is { ConnectionState: NatsConnectionState.Open };

    protected override void Build()
    {
        base.Build();
        AddInit(CreateConnectionAsync());
    }

    private Task CreateConnectionAsync()
    {
        try
        {
            _connection = new NatsConnection(new NatsOpts
            {
                Url = ProviderSettings.Endpoint,
                LoggerFactory = LoggerFactory,
                AuthOpts = ProviderSettings.AuthOpts,
                Verbose = false,
                Name = ProviderSettings.ClientName
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not initialize Nats connection: {ErrorMessage}", e.Message);
        }

        return Task.CompletedTask;
    }

    protected override async Task CreateConsumers()
    {
        if (_connection == null)
        {
            throw new ConsumerMessageBusException("The connection is not available at this time");
        }

        await base.CreateConsumers();

        foreach (var (subject, consumerSettings) in Settings.Consumers.GroupBy(x => x.Path).ToDictionary(x => x.Key, x => x.ToList()))
        {
            var processor = new MessageProcessor<NatsMsg<byte[]>>(
                consumerSettings,
                messageBus: this,
                messageProvider: (type, message) => Serializer.Deserialize(type, message.Data),
                subject,
                this,
                consumerErrorHandlerOpenGenericType: typeof(INatsConsumerErrorHandler<>));

            AddSubjectConsumer(subject, processor);
        }

        if (Settings.RequestResponse != null)
        {
            var processor = new ResponseMessageProcessor<NatsMsg<byte[]>>(LoggerFactory, Settings.RequestResponse, this, message => message.Data);
            AddSubjectConsumer(Settings.RequestResponse.Path, processor);
        }
    }

    private void AddSubjectConsumer(string subject, IMessageProcessor<NatsMsg<byte[]>> processor)
    {
        _logger.LogInformation("Creating consumer for {Subject}", subject);
        var consumer = new NatsSubjectConsumer<byte[]>(LoggerFactory.CreateLogger<NatsSubjectConsumer<byte[]>>(), subject, _connection, processor);
        AddConsumer(consumer);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore().ConfigureAwait(false);

        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    protected override async Task<ProduceToTransportBulkResult<T>> ProduceToTransportBulk<T>(IReadOnlyCollection<T> envelopes, string path, IMessageBusTarget targetBus,
        CancellationToken cancellationToken)
    {

        await EnsureInitFinished();

        if (_connection == null)
        {
            throw new ProducerMessageBusException("The connection is not available at this time");
        }

        var messages = envelopes.Select(envelope =>
        {
            var messagePayload = Serializer.Serialize(envelope.MessageType, envelope.Message);

            var replyTo = envelope.Headers.TryGetValue("ReplyTo", out var replyToValue) ? replyToValue.ToString() : null;

            NatsMsg<byte[]> m = new()
            {
                Data = messagePayload,
                Subject = path,
                Headers = [],
                ReplyTo = replyTo
            };

            foreach (var header in envelope.Headers)
            {
                m.Headers.Add(new KeyValuePair<string, StringValues>(header.Key, header.Value.ToString()));
            }

            return (Envelope: envelope, Message: m);
        });

        var dispatched = new List<T>(envelopes.Count);
        foreach (var item in messages)
        {
            try
            {
                await _connection.PublishAsync(item.Message, cancellationToken: cancellationToken);
                dispatched.Add(item.Envelope);
            }
            catch (Exception ex)
            {
                return new ProduceToTransportBulkResult<T>(dispatched, ex);
            }
        }

        return new ProduceToTransportBulkResult<T>(dispatched, null);
    }
}