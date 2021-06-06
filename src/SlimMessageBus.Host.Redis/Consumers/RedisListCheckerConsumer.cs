namespace SlimMessageBus.Host.Redis
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
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

        public async Task Finish()
        {
            if (task == null)
            {
                return;
            }

            cancellationTokenSource?.Cancel();
            await task.ConfigureAwait(false);
            task = null;
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

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (cancellationTokenSource != null)
                {
                    cancellationTokenSource.Dispose();
                    cancellationTokenSource = null;
                }

                queues.SelectMany(x => x.Processors).ToList().ForEach(x => x.DisposeSilently());
                queues.Clear();
            }
        }

        #endregion
    }
}