namespace SlimMessageBus.Host.Collections;

public interface IAsyncTaskList
{
    void Add(Func<Task> taskFactory, CancellationToken cancellationToken);
    Task EnsureAllFinished();
}

/// <summary>
/// Tracks a list of Tasks that have to be awaited before we can proceed.
/// </summary>
public class AsyncTaskList : IAsyncTaskList
{
    private readonly object _currentTaskLock = new();
    private Task _currentTask = null;
    private Func<Task> _currentTaskFactory;

    public void Add(Func<Task> taskFactory, CancellationToken cancellationToken)
    {
        static async Task AddNext(Task prevTask, Func<Task> taskFactory, CancellationToken cancellationToken)
        {
            if (prevTask != null)
            {
                await prevTask.ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            await taskFactory();
        }

        lock (_currentTaskLock)
        {
            var prevFactory = _currentTaskFactory;
            Task task = null;
            // Each entry caches success independently, so retrying a failed chain does not
            // repeat initialization steps that have already finished successfully.
            Task GetOrStartTask()
            {
                if (task == null || task.IsFaulted || task.IsCanceled)
                {
                    task = AddNext(prevFactory?.Invoke(), taskFactory, cancellationToken);
                }
                return task;
            }
            _currentTaskFactory = GetOrStartTask;
            _currentTask = GetOrStartTask();
        }
    }


    /// <summary>
    /// Awaits (if any) bus intialization tasks (e.g. topology provisining) before we can produce message into the bus (or consume messages).
    /// </summary>
    /// <returns></returns>
    public async Task EnsureAllFinished()
    {
        Task initTask;
        lock (_currentTaskLock)
        {
            initTask = _currentTask = _currentTaskFactory?.Invoke();
        }
        if (initTask != null)
        {
            await initTask.ConfigureAwait(false);

            lock (_currentTaskLock)
            {
                // Clear if await finished and the current task chain was the one we awaited
                if (ReferenceEquals(_currentTask, initTask))
                {
                    _currentTask = null;
                    _currentTaskFactory = null;
                }
            }
        }
    }


}
