namespace SlimMessageBus.Host.Redis;

using System.Diagnostics;

public class RedisListCheckerConsumer : AbstractConsumer, IRedisConsumer
{
    private readonly IDatabase _database;
    private readonly IList<QueueProcessors> _queues;
    private readonly TimeSpan? _pollDelay;
    private readonly TimeSpan _maxIdle;
    private readonly IMessageSerializer _envelopeSerializer;
    private Task _task;

    protected class QueueProcessors
    {
        public string Name { get; }
        public List<IMessageProcessor<MessageWithHeaders>> Processors { get; }

        public QueueProcessors(string name, List<IMessageProcessor<MessageWithHeaders>> processors)
        {
            Name = name;
            Processors = processors;
        }
    }

    public RedisListCheckerConsumer(ILogger<RedisListCheckerConsumer> logger, IDatabase database, TimeSpan? pollDelay, TimeSpan maxIdle, IEnumerable<(string QueueName, IMessageProcessor<MessageWithHeaders> Processor)> queues, IMessageSerializer envelopeSerializer)
        : base(logger)
    {
        _database = database;
        _pollDelay = pollDelay;
        _maxIdle = maxIdle;
        _envelopeSerializer = envelopeSerializer;
        _queues = queues.GroupBy(x => x.QueueName, x => x.Processor).Select(x => new QueueProcessors(x.Key, x.ToList())).ToList();
    }

    protected override Task OnStart()
    {
        _task = Run();
        return Task.CompletedTask;
    }

    protected override async Task OnStop()
    {
        await _task.ConfigureAwait(false);
        _task = null;
    }

    protected async Task Run()
    {
        var idle = Stopwatch.StartNew();

        while (!CancellationToken.IsCancellationRequested)
        {
            Logger.LogTrace("Checking keys...");

            var itemsArrived = false;

            // for loop to avoid iterator allocation
            for (var queueIndex = 0; queueIndex < _queues.Count; queueIndex++)
            {
                var queue = _queues[queueIndex];

                var value = await _database.ListLeftPopAsync(queue.Name).ConfigureAwait(false);
                if (value != RedisValue.Null)
                {
                    Logger.LogDebug("Retrieved value on queue {Queue}", queue.Name);
                    try
                    {
                        var transportMessage = (MessageWithHeaders)_envelopeSerializer.Deserialize(typeof(MessageWithHeaders), value);

                        // for loop to avoid iterator allocation
                        for (var i = 0; i < queue.Processors.Count && !CancellationToken.IsCancellationRequested; i++)
                        {
                            var processor = queue.Processors[i];

                            var (exception, _, _, _) = await processor.ProcessMessage(transportMessage, transportMessage.Headers, CancellationToken).ConfigureAwait(false);
                            if (exception != null)
                            {
                                Logger.LogError(exception, "Error occured while processing the list item on {Queue}", queue.Name);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, "Error occured while processing the list item on {Queue}", queue.Name);
                    }

                    itemsArrived = true;
                    idle.Restart();
                }
            }

            if (!itemsArrived && _pollDelay != null && idle.Elapsed >= _maxIdle && !CancellationToken.IsCancellationRequested)
            {
                Logger.LogTrace("Performing delay since no new items arrived");
                await Task.Delay(_pollDelay.Value).ConfigureAwait(false);
            }
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore();

        var processors = _queues.SelectMany(x => x.Processors).ToList();
        foreach (var processor in processors)
        {
            if (processor is IDisposable disposable)
            {
                disposable.DisposeSilently();
            }
        }
        _queues.Clear();
    }
}