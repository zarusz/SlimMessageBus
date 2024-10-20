namespace SlimMessageBus.Host.Mqtt;

public class MqttTopicConsumer : AbstractConsumer
{
    public IMessageProcessor<MqttApplicationMessage> MessageProcessor { get; }
    public string Topic { get; }

    public MqttTopicConsumer(ILogger logger, IEnumerable<AbstractConsumerSettings> consumerSettings, string topic, IMessageProcessor<MqttApplicationMessage> messageProcessor) 
        : base(logger, consumerSettings)
    {
        Topic = topic;
        MessageProcessor = messageProcessor;
    }

    protected override Task OnStart() => Task.CompletedTask;

    protected override Task OnStop() => Task.CompletedTask;
}
