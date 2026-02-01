namespace SlimMessageBus.Host.RabbitMQ;

using global::RabbitMQ.Client;

public class RabbitMqTopologyService
{
    private readonly ILogger _logger;
    private readonly IChannel _channel;
    private readonly MessageBusSettings _settings;
    private readonly RabbitMqMessageBusSettings _providerSettings;

    public RabbitMqTopologyService(ILoggerFactory loggerFactory, IChannel channel, MessageBusSettings settings, RabbitMqMessageBusSettings providerSettings)
    {
        _logger = loggerFactory.CreateLogger<RabbitMqTopologyService>();
        _channel = channel;
        _settings = settings;
        _providerSettings = providerSettings;
    }

    public async Task<bool> ProvisionTopology()
    {
        try
        {
            await ProvisionProducers();
            await ProvisionConsumers();
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not create topology");
            return false;
        }
    }

    private async Task ProvisionConsumers()
    {
        foreach (var consumer in _settings.Consumers)
        {
            var deadLetterExchange = consumer.GetOrDefault<string>(RabbitMqProperties.DeadLetterExchange, _providerSettings, null);
            var deadLetterExchangeType = consumer.GetOrDefault<string>(RabbitMqProperties.DeadLetterExchangeType, _providerSettings, null);
            var deadLetterExchangeRoutingKey = consumer.GetOrDefault<string>(RabbitMqProperties.DeadLetterExchangeRoutingKey, _providerSettings, null);
            var deadLetterExchangeDurable = consumer.GetOrDefault(RabbitMqProperties.DeadLetterExchangeDurable, _providerSettings, false);
            var deadLetterExchangeAutoDelete = consumer.GetOrDefault(RabbitMqProperties.DeadLetterExchangeAutoDelete, _providerSettings, false);

            // Declare DeadLetter exchange when details provided
            if (deadLetterExchange != null && deadLetterExchangeType != null)
            {
                await DeclareExchange(deadLetterExchange, exchangeType: deadLetterExchangeType, durable: deadLetterExchangeDurable, autoDelete: deadLetterExchangeAutoDelete);
            }

            // ToDo: Ability to create server generated queue names
            var queueName = consumer.GetQueueName();
            await DeclareQueue(consumer, queueName: queueName, arguments =>
            {
                if (deadLetterExchange != null)
                {
                    arguments.Add("x-dead-letter-exchange", deadLetterExchange);
                }
                if (deadLetterExchangeRoutingKey != null)
                {
                    arguments.Add("x-dead-letter-routing-key", deadLetterExchangeRoutingKey);
                }
            });

            await DeclareQueueBinding(consumer, consumer.Path, queueName);
        }

        if (_settings.RequestResponse != null)
        {
            var exchangeName = _settings.RequestResponse.Path;
            await DeclareExchange(_settings.RequestResponse, exchangeName: exchangeName);

            var queueName = _settings.RequestResponse.GetQueueName();
            await DeclareQueue(_settings.RequestResponse, queueName: queueName);
            await DeclareQueueBinding(_settings.RequestResponse, bindingExchangeName: exchangeName, queueName);
        }
    }

    private async Task DeclareQueueBinding(AbstractConsumerSettings settings, string bindingExchangeName, string queueName)
    {
        var bindingRoutingKey = settings.GetBindingRoutingKey(_providerSettings) ?? string.Empty;

        if (string.IsNullOrEmpty(bindingExchangeName))
        {
            _logger.LogInformation("Skipping binding for queue {QueueName} because exchange is default (empty string)", queueName);
            return;
        }

        _logger.LogInformation("Binding queue {QueueName} to exchange {ExchangeName} using routing key {RoutingKey}", queueName, bindingExchangeName, bindingRoutingKey);
        try
        {
            await _channel.QueueBindAsync(queueName, bindingExchangeName, routingKey: bindingRoutingKey, arguments: null);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to bind queue {QueueName} to exchange {ExchangeName}", queueName, bindingExchangeName);
        }
    }

    private async Task ProvisionProducers()
    {
        foreach (var producer in _settings.Producers)
        {
            await DeclareExchange(producer, exchangeName: producer.DefaultPath);
        }
    }

    /// <summary>
    /// Declares the queue
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="queueName"></param>
    /// <param name="argumentModifier"></param>
    /// <returns>The queue name</returns>
    private async Task<string> DeclareQueue(HasProviderExtensions settings, string queueName, Action<IDictionary<string, object>> argumentModifier = null)
    {
        var queueDurable = settings.GetOrDefault(RabbitMqProperties.QueueDurable, _providerSettings, false);
        var queueAutoDelete = settings.GetOrDefault(RabbitMqProperties.QueueAutoDelete, _providerSettings, false);
        var queueExclusive = settings.GetOrDefault(RabbitMqProperties.QueueExclusive, _providerSettings, false);
        var queueArguments = settings.GetOrDefault<IDictionary<string, object>>(RabbitMqProperties.QueueArguments, _providerSettings, null);

        _logger.LogInformation("Declaring queue {QueueName}, Durable: {Durable}, AutoDelete: {AutoDelete}, Exclusive: {Exclusive}", queueName, queueDurable, queueAutoDelete, queueExclusive);
        try
        {
            var arguments = queueArguments;
            if (argumentModifier != null)
            {
                arguments = arguments != null
                    ? new Dictionary<string, object>(queueArguments) // make a copy of the arguments for the modifier to mutate
                    : [];
                argumentModifier(arguments);
            }

            await _channel.QueueDeclareAsync(queueName, durable: queueDurable, exclusive: queueExclusive, autoDelete: queueAutoDelete, arguments: arguments);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to declare queue {QueueName}", queueName);
        }

        return queueName;
    }

    private async Task DeclareExchange(HasProviderExtensions settings, string exchangeName)
    {
        if (string.IsNullOrEmpty(exchangeName))
        {
            _logger.LogInformation("Skipping exchange declaration because exchange name is default (empty string)");
            return;
        }

        var exchangeType = settings.GetOrDefault(RabbitMqProperties.ExchangeType, _providerSettings, global::RabbitMQ.Client.ExchangeType.Fanout);
        var durable = settings.GetOrDefault(RabbitMqProperties.ExchangeDurable, _providerSettings, false);
        var autoDelete = settings.GetOrDefault(RabbitMqProperties.ExchangeAutoDelete, _providerSettings, false);
        var arguments = settings.GetOrDefault<IDictionary<string, object>>(RabbitMqProperties.ExchangeArguments, _providerSettings, null);

        await DeclareExchange(exchangeName: exchangeName, exchangeType: exchangeType, durable: durable, autoDelete: autoDelete, arguments: arguments);
    }

    private async Task DeclareExchange(string exchangeName, string exchangeType, bool durable, bool autoDelete, IDictionary<string, object> arguments = null)
    {
        if (string.IsNullOrEmpty(exchangeName))
        {
            _logger.LogInformation("Skipping exchange declaration because exchange name is default (empty string)");
            return;
        }

        _logger.LogInformation("Declaring exchange {ExchangeName}, ExchangeType: {ExchangeType}, Durable: {Durable}, AutoDelete: {AutoDelete}", exchangeName, exchangeType, durable, autoDelete);
        try
        {
            await _channel.ExchangeDeclareAsync(exchangeName, exchangeType, durable: durable, autoDelete: autoDelete, arguments: arguments);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to declare exchange {ExchangeName}", exchangeName);
        }
    }
}