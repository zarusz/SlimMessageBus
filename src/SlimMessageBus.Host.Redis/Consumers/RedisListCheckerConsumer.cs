namespace SlimMessageBus.Host.Redis;

using System;
using System.Diagnostics;
using SlimMessageBus.Host.Serialization;
using StackExchange.Redis;

public class RedisListCheckerConsumer : IRedisConsumer
{
    private readonly ILogger<RedisListCheckerConsumer> _logger;
    private readonly IDatabase _database;
    private readonly IList<QueueProcessors> _queues;
    private readonly TimeSpan? _pollDelay;
    private readonly TimeSpan _maxIdle;
    private readonly IMessageSerializer _envelopeSerializer;
    private CancellationTokenSource _cancellationTokenSource;
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
    {
        _logger = logger;
        _database = database;
        _pollDelay = pollDelay;
        _maxIdle = maxIdle;
        _envelopeSerializer = envelopeSerializer;
        _queues = queues.GroupBy(x => x.QueueName, x => x.Processor).Select(x => new QueueProcessors(x.Key, x.ToList())).ToList();
    }

    public async Task Start()
    {
        if (_task != null)
        {
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _task = await Task.Factory.StartNew(() => Run(), _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
    }

    public async Task Stop()
    {
        if (_task == null)
        {
            return;
        }

        _cancellationTokenSource.Cancel();

        await _task.ConfigureAwait(false);
        _task = null;

        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
    }

    protected async Task Run()
    {
        var idle = Stopwatch.StartNew();

        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            _logger.LogTrace("Checking keys...");

            var itemsArrived = false;

            // for loop to avoid iterator allocation
            for (var queueIndex = 0; queueIndex < _queues.Count; queueIndex++)
            {
                var queue = _queues[queueIndex];

                var value = await _database.ListLeftPopAsync(queue.Name).ConfigureAwait(false);
                if (value != RedisValue.Null)
                {
                    _logger.LogDebug("Retrieved value on queue {Queue}", queue.Name);
                    try
                    {
                        var transportMessage = (MessageWithHeaders)_envelopeSerializer.Deserialize(typeof(MessageWithHeaders), value);

                        // for loop to avoid iterator allocation
                        for (var i = 0; i < queue.Processors.Count; i++)
                        {
                            var processor = queue.Processors[i];

                            var (exception, _, _) = await processor.ProcessMessage(transportMessage, transportMessage.Headers).ConfigureAwait(false);
                            if (exception != null)
                            {
                                _logger.LogError(exception, "Error occured while processing the list item on {Queue}", queue.Name);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error occured while processing the list item on {Queue}", queue.Name);
                    }

                    itemsArrived = true;
                    idle.Restart();
                }
            }

            if (!itemsArrived && _pollDelay != null && idle.Elapsed >= _maxIdle)
            {
                _logger.LogTrace("Performing delay since no new items arrived");
                await Task.Delay(_pollDelay.Value).ConfigureAwait(false);
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

        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        var processors = _queues.SelectMany(x => x.Processors).ToList();
        foreach (var processor in processors)
        {
            await processor.DisposeSilently();
        }
        _queues.Clear();
    }

    #endregion
}