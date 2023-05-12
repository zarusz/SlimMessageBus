namespace SlimMessageBus.Host.RabbitMQ;

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
            var queueName = consumer.GetOrDefault<string>(RabbitMqProperties.QueueName, _providerSettings, null);
            var queueDurable = consumer.GetOrDefault(RabbitMqProperties.QueueDurable, _providerSettings, false);
            var queueAutoDelete = consumer.GetOrDefault(RabbitMqProperties.QueueAutoDelete, _providerSettings, false);
            var queueExclusive = consumer.GetOrDefault(RabbitMqProperties.QueueExclusive, _providerSettings, false);
            var queueArguments = consumer.GetOrDefault<IDictionary<string, object>>(RabbitMqProperties.QueueArguments, _providerSettings, null);

            var bindingExchangeName = consumer.Path;
            var bindingRoutingKey = consumer.GetOrDefault(RabbitMqProperties.BindingRoutingKey, _providerSettings, string.Empty);

            var deadLetterExchange = consumer.GetOrDefault<string>(RabbitMqProperties.DeadLetterExchange, _providerSettings, null);
            var deadLetterExchangeType = consumer.GetOrDefault<string>(RabbitMqProperties.DeadLetterExchangeType, _providerSettings, null);
            var deadLetterExchangeRoutingKey = consumer.GetOrDefault<string>(RabbitMqProperties.DeadLetterExchangeRoutingKey, _providerSettings, null);
            var deadLetterExchangeDurable = consumer.GetOrDefault(RabbitMqProperties.DeadLetterExchangeDurable, _providerSettings, false);
            var deadLetterExchangeAutoDelete = consumer.GetOrDefault(RabbitMqProperties.DeadLetterExchangeAutoDelete, _providerSettings, false);

            // ToDo: Add ability to create exchanges from consumers
            // ToDo: Ability to create server generated queue names

            _logger.LogInformation("Declaring queue {QueueName}, Durable: {Durable}, AutoDelete: {AutoDelete}, Exclusive: {Exclusive}", queueName, queueDurable, queueAutoDelete, queueExclusive);
            try
            {
                var arguments = new Dictionary<string, object>(queueArguments ?? Enumerable.Empty<KeyValuePair<string, object>>());
                if (deadLetterExchange != null)
                {
                    arguments.Add("x-dead-letter-exchange", deadLetterExchange);
                }
                if (deadLetterExchangeRoutingKey != null)
                {
                    arguments.Add("x-dead-letter-routing-key", deadLetterExchangeRoutingKey);
                }

                _channel.QueueDeclare(queueName, durable: queueDurable, exclusive: queueExclusive, autoDelete: queueAutoDelete, arguments: arguments);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to declare queue {QueueName}", queueName);
            }

            _logger.LogInformation("Binding queue {QueueName} to exchange {ExchangeName} using routing key {RoutingKey}", queueName, bindingExchangeName, bindingRoutingKey);
            try
            {
                _channel.QueueBind(queueName, bindingExchangeName, routingKey: bindingRoutingKey, arguments: null);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to bind queue {QueueName} to exchange {ExchangeName}", queueName, bindingExchangeName);
            }

            // Declare DeadLetter exchange when details provided
            if (deadLetterExchange != null && deadLetterExchangeType != null)
            {
                DeclareExchange(deadLetterExchange, exchangeType: deadLetterExchangeType, durable: deadLetterExchangeDurable, autoDelete: deadLetterExchangeAutoDelete);
            }
        }
    }

    private void ProvisionProducers()
    {
        foreach (var producer in _settings.Producers)
        {
            var exchangeType = producer.GetOrDefault(RabbitMqProperties.ExchangeType, _providerSettings, global::RabbitMQ.Client.ExchangeType.Fanout);
            var durable = producer.GetOrDefault(RabbitMqProperties.ExchangeDurable, _providerSettings, false);
            var autoDelete = producer.GetOrDefault(RabbitMqProperties.ExchangeAutoDelete, _providerSettings, false);
            var arguments = producer.GetOrDefault<IDictionary<string, object>>(RabbitMqProperties.ExchangeArguments, _providerSettings, null);

            DeclareExchange(producer.DefaultPath, exchangeType: exchangeType, durable: durable, autoDelete: autoDelete, arguments: arguments);
        }
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