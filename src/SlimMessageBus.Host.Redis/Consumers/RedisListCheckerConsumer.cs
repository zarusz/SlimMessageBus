namespace SlimMessageBus.Host.Redis;

using System.Diagnostics;
using StackExchange.Redis;

public class RedisListCheckerConsumer : IRedisConsumer
{
    private readonly ILogger<RedisListCheckerConsumer> logger;
    private readonly IDatabase database;
    private readonly IList<QueueProcessors> queues;
    private readonly TimeSpan? pollDelay;
    private readonly TimeSpan maxIdle;

    private CancellationTokenSource cancellationTokenSource;
    private Task task;

    protected class QueueProcessors
    {
        public string Name { get; }
        public List<IMessageProcessor<byte[]>> Processors { get; }

        public QueueProcessors(string name, List<IMessageProcessor<byte[]>> processors)
        {
            Name = name;
            Processors = processors;
        }
    }

    public RedisListCheckerConsumer(ILogger<RedisListCheckerConsumer> logger, IDatabase database, TimeSpan? pollDelay, TimeSpan maxIdle, IEnumerable<(string QueueName, IMessageProcessor<byte[]> Processor)> queues)
    {
        this.logger = logger;
        this.database = database;
        this.pollDelay = pollDelay;
        this.maxIdle = maxIdle;
        this.queues = queues.GroupBy(x => x.QueueName, x => x.Processor).Select(x => new QueueProcessors(x.Key, x.ToList())).ToList();
    }

    public async Task Start()
    {
        if (task != null)
        {
            return;
        }

        cancellationTokenSource = new CancellationTokenSource();
        task = await Task.Factory.StartNew(() => Run(), cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
    }

    public async Task Stop()
    {
        if (task == null)
        {
            return;
        }

        cancellationTokenSource.Cancel();

        await task.ConfigureAwait(false);
        task = null;

        cancellationTokenSource.Dispose();
        cancellationTokenSource = null;
    }

    protected async Task Run()
    {
        var idle = Stopwatch.StartNew();

        while (!cancellationTokenSource.Token.IsCancellationRequested)
        {
            logger.LogTrace("Checking keys...");

            var itemsArrived = false;

            // for loop to avoid iterator allocation
            for (var queueIndex = 0; queueIndex < queues.Count; queueIndex++)
            {
                var queue = queues[queueIndex];

                var value = await database.ListLeftPopAsync(queue.Name).ConfigureAwait(false);
                if (value != RedisValue.Null)
                {
                    logger.LogDebug("Retrieved value on queue {Queue}", queue.Name);

                    // for loop to avoid iterator allocation
                    for (var i = 0; i < queue.Processors.Count; i++)
                    {
                        var processor = queue.Processors[i];

                        await processor.ProcessMessage(value).ConfigureAwait(false);
                    }

                    itemsArrived = true;
                    idle.Restart();
                }
            }

            if (!itemsArrived && pollDelay != null && idle.Elapsed >= maxIdle)
            {
                logger.LogTrace("Performing delay since no new items arrived");
                await Task.Delay(pollDelay.Value).ConfigureAwait(false);
            }
        }
    }

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        await Stop();

        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }

        var processors = queues.SelectMany(x => x.Processors).ToList();
        foreach (var processor in processors)
        {
            await processor.DisposeSilently();
        }
        queues.Clear();
    }

    #endregion
}