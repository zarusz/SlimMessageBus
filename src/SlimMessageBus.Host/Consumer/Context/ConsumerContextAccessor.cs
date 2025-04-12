namespace SlimMessageBus.Host;

public sealed class ConsumerContextAccessor : IConsumerContextAccessor
{
    private static readonly AsyncLocal<ConsumerContextHolder> _currentContext = new AsyncLocal<ConsumerContextHolder>();

    public IConsumerContext? ConsumerContext
    {
        get => _currentContext.Value?.Context;
        set
        {
            var holder = _currentContext.Value;
            if (holder != null)
            {
                // Clear current Context trapped in the AsyncLocals, as its done.
                holder.Context = null;
            }

            if (value != null)
            {
                // Use an object indirection to hold the Context in the AsyncLocal,
                // so it can be cleared in all ExecutionContexts when its cleared.
                _currentContext.Value = new ConsumerContextHolder { Context = value };
            }
        }
    }

    private sealed class ConsumerContextHolder
    {
        public IConsumerContext? Context;
    }
}