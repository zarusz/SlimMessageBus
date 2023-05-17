namespace SlimMessageBus.Host.RabbitMQ;

using System.Runtime;

using global::RabbitMQ.Client;

public class RabbitMqTopologyService
{
    private readonly ILogger _logger;
    private readonly IModel _channel;
    private readonly MessageBusSettings _settings;
    private readonly RabbitMqMessageBusSettings _providerSettings;

    public RabbitMqTopologyService(ILoggerFactory loggerFactory, IModel channel, MessageBusSettings settings, RabbitMqMessageBusSettings providerSettings)
    {
        _logger = loggerFactory.CreateLogger<RabbitMqTopologyService>();
        _channel = channel;
        _settings = settings;
        _providerSettings = providerSettings;
    }

    public bool ProvisionTopology()
    {
        try
        {
            ProvisionProducers();
            ProvisionConsumers();
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not create topology");
            return false;
        }
    }

    private void ProvisionConsumers()
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
                DeclareExchange(deadLetterExchange, exchangeType: deadLetterExchangeType, durable: deadLetterExchangeDurable, autoDelete: deadLetterExchangeAutoDelete);
            }

            // ToDo: Ability to create server generated queue names
            var queueName = consumer.GetQueueName();
            DeclareQueue(consumer, queueName: queueName, arguments =>
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

            DeclareQueueBinding(consumer, consumer.Path, queueName);
        }

        if (_settings.RequestResponse != null)
        {
            var exchangeName = _settings.RequestResponse.Path;
            DeclareExchange(_settings.RequestResponse, exchangeName: exchangeName);

            var queueName = _settings.RequestResponse.GetQueueName();
            DeclareQueue(_settings.RequestResponse, queueName: queueName);
            DeclareQueueBinding(_settings.RequestResponse, bindingExchangeName: exchangeName, queueName);
        }
    }

    private void DeclareQueueBinding(HasProviderExtensions settings, string bindingExchangeName, string queueName)
    {
        var bindingRoutingKey = settings.GetOrDefault(RabbitMqProperties.BindingRoutingKey, _providerSettings, string.Empty);

        _logger.LogInformation("Binding queue {QueueName} to exchange {ExchangeName} using routing key {RoutingKey}", queueName, bindingExchangeName, bindingRoutingKey);
        try
        {
            _channel.QueueBind(queueName, bindingExchangeName, routingKey: bindingRoutingKey, arguments: null);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to bind queue {QueueName} to exchange {ExchangeName}", queueName, bindingExchangeName);
        }
    }

    private void ProvisionProducers()
    {
        foreach (var producer in _settings.Producers)
        {
            DeclareExchange(producer, exchangeName: producer.DefaultPath);
        }
    }

    /// <summary>
    /// Declares the queue
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="argumentModifier"></param>
    /// <returns>The queue name</returns>
    private string DeclareQueue(HasProviderExtensions settings, string queueName, Action<IDictionary<string, object>> argumentModifier = null)
    {
        var queueDurable = settings.GetOrDefault(RabbitMqProperties.QueueDurable, _providerSettings, false);
        var queueAutoDelete = settings.GetOrDefault(RabbitMqProperties.QueueAutoDelete, _providerSettings, false);
        var queueExclusive = settings.GetOrDefault(RabbitMqProperties.QueueExclusive, _providerSettings, false);
        var queueArguments = settings.GetOrDefault<IDictionary<string, object>>(RabbitMqProperties.QueueArguments, _providerSettings, null);

        _logger.LogInformation("Declaring queue {QueueName}, Durable: {Durable}, AutoDelete: {AutoDelete}, Exclusive: {Exclusive}", queueName, queueDurable, queueAutoDelete, queueExclusive);
        try
        {
            var arguments = new Dictionary<string, object>(queueArguments ?? Enumerable.Empty<KeyValuePair<string, object>>());
            argumentModifier?.Invoke(arguments);

            _channel.QueueDeclare(queueName, durable: queueDurable, exclusive: queueExclusive, autoDelete: queueAutoDelete, arguments: arguments);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to declare queue {QueueName}", queueName);
        }

        return queueName;
    }

    private void DeclareExchange(HasProviderExtensions settings, string exchangeName)
    {
        var exchangeType = settings.GetOrDefault(RabbitMqProperties.ExchangeType, _providerSettings, global::RabbitMQ.Client.ExchangeType.Fanout);
        var durable = settings.GetOrDefault(RabbitMqProperties.ExchangeDurable, _providerSettings, false);
        var autoDelete = settings.GetOrDefault(RabbitMqProperties.ExchangeAutoDelete, _providerSettings, false);
        var arguments = settings.GetOrDefault<IDictionary<string, object>>(RabbitMqProperties.ExchangeArguments, _providerSettings, null);

        DeclareExchange(exchangeName: exchangeName, exchangeType: exchangeType, durable: durable, autoDelete: autoDelete, arguments: arguments);
    }

    private void DeclareExchange(string exchangeName, string exchangeType, bool durable, bool autoDelete, IDictionary<string, object> arguments = null)
    {
        _logger.LogInformation("Declaring exchange {ExchangeName}, ExchangeType: {ExchangeType}, Durable: {Durable}, AutoDelete: {AutoDelete}", exchangeName, exchangeType, durable, autoDelete);
        try
        {
            _channel.ExchangeDeclare(exchangeName, exchangeType, durable: durable, autoDelete: autoDelete, arguments: arguments);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to declare exchange {ExchangeName}", exchangeName);
        }
    }
}