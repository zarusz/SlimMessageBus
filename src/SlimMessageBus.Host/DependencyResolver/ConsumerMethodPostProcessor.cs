namespace SlimMessageBus.Host;

public class ConsumerMethodPostProcessor : IMessageBusSettingsPostProcessor
{
    public void Run(MessageBusSettings settings)
    {
        var consumerInvokers = settings.Consumers.Concat(settings.Children.SelectMany(x => x.Consumers))
            .SelectMany(x => x.Invokers).ToList();
        foreach (var consumerInvoker in consumerInvokers.Where(x => x.ConsumerMethod == null && x.ConsumerMethodInfo != null))
        {
            consumerInvoker.ConsumerMethod = ReflectionUtils.GenerateMethodCallToFunc<Func<object, object, IConsumerContext, CancellationToken, Task>>(consumerInvoker.ConsumerMethodInfo, consumerInvoker.MessageType);
        }
    }
}