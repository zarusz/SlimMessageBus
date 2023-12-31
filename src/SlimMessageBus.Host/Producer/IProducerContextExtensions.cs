namespace SlimMessageBus.Host;

public static class IProducerContextExtensions
{
    public static IMasterMessageBus GetMasterMessageBus(this IProducerContext context)
    {
        var busTarget = context.Bus as IMessageBusTarget;
        return busTarget?.Target as IMasterMessageBus ?? context.Bus as IMasterMessageBus;
    }
}
