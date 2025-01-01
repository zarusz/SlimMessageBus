namespace SlimMessageBus.Host.Mqtt;

public class MqttTopicConsumer : AbstractConsumer
{
    public IMessageProcessor<MqttApplicationMessage> MessageProcessor { get; }

    public MqttTopicConsumer(ILogger logger,
                             IEnumerable<AbstractConsumerSettings> consumerSettings,
                             IEnumerable<IAbstractConsumerInterceptor> interceptors,
                             string topic,
                             IMessageProcessor<MqttApplicationMessage> messageProcessor)
        : base(logger,
               consumerSettings,
               topic,
               interceptors)
    {
        MessageProcessor = messageProcessor;
    }

    protected override Task OnStart() => Task.CompletedTask;

    protected override Task OnStop() => Task.CompletedTask;
}
