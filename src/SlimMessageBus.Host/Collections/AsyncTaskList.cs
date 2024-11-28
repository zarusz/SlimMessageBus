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

    public void Add(Func<Task> taskFactory, CancellationToken cancellationToken)
    {
        static async Task AddNext(Task prevTask, Func<Task> taskFactory)
        {
            await prevTask;
            await taskFactory();
        }

        lock (_currentTaskLock)
        {
            var prevTask = _currentTask;
            _currentTask = prevTask != null
                ? AddNext(prevTask, taskFactory)
                : taskFactory();
        }
    }


    /// <summary>
    /// Awaits (if any) bus intialization tasks (e.g. topology provisining) before we can produce message into the bus (or consume messages).
    /// </summary>
    /// <returns></returns>
    public async Task EnsureAllFinished()
    {
        var initTask = _currentTask;
        if (initTask != null)
        {
            await initTask.ConfigureAwait(false);

            lock (_currentTaskLock)
            {
                // Clear if await finished and the current task chain was the one we awaited
                if (ReferenceEquals(_currentTask, initTask))
                {
                    _currentTask = null;
                }
            }
        }
    }


}