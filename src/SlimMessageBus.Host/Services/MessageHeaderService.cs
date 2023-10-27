namespace SlimMessageBus.Host.Services;

using System.Runtime;

internal interface IMessageHeaderService
{
    void AddMessageHeaders(IDictionary<string, object> messageHeaders, IDictionary<string, object> headers, object message, ProducerSettings producerSettings);
    void AddMessageTypeHeader(object message, IDictionary<string, object> headers);
}

internal class MessageHeaderService : IMessageHeaderService
{
    private readonly ILogger _logger;
    private readonly MessageBusSettings _settings;
    private readonly IMessageTypeResolver _messageTypeResolver;

    public MessageHeaderService(ILogger logger, MessageBusSettings settings, IMessageTypeResolver messageTypeResolver)
    {
        _logger = logger;
        _settings = settings;
        _messageTypeResolver = messageTypeResolver;
    }

    public void AddMessageHeaders(IDictionary<string, object> messageHeaders, IDictionary<string, object> headers, object message, ProducerSettings producerSettings)
    {
        if (headers != null)
        {
            // Add user specific headers
            foreach (var header in headers)
            {
                messageHeaders[header.Key] = header.Value;
            }
        }

        AddMessageTypeHeader(message, messageHeaders);

        if (_settings.HeaderModifier != null)
        {
            // Call header hook
            _logger.LogTrace($"Executing bus {nameof(MessageBusSettings.HeaderModifier)}");
            _settings.HeaderModifier(messageHeaders, message);
        }

        if (producerSettings.HeaderModifier != null)
        {
            // Call header hook        
            _logger.LogTrace($"Executing producer {nameof(ProducerSettings.HeaderModifier)}");
            producerSettings.HeaderModifier(messageHeaders, message);
        }
    }

    public void AddMessageTypeHeader(object message, IDictionary<string, object> headers)
    {
        if (message != null)
        {
            headers.SetHeader(MessageHeaders.MessageType, _messageTypeResolver.ToName(message.GetType()));
        }
    }
}

