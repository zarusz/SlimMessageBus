namespace SlimMessageBus.Host.Mqtt;

public class MqttTopicConsumer : AbstractConsumer
{
    public IMessageProcessor<MqttApplicationMessage> MessageProcessor { get; }
    public string Topic { get; }

    public MqttTopicConsumer(ILogger logger, string topic, IMessageProcessor<MqttApplicationMessage> messageProcessor) : base(logger)
    {
        Topic = topic;
        MessageProcessor = messageProcessor;
    }

    protected override Task OnStart() => Task.CompletedTask;

    protected override Task OnStop() => Task.CompletedTask;
}
