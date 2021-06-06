namespace SlimMessageBus.Host
{
    using System.Threading.Tasks;

    public class TaskMarker
    {
        public bool CanRun { get; set; } = true;
        public bool IsRunning { get; set; } = false;

        public Task Stop()
        {
            CanRun = false;

            if (!IsRunning)
            {
                return Task.CompletedTask;
            }

            return Task.Run(() =>
            {
                while (IsRunning)
                {
                    Task.Delay(100).Wait();
                }
            });
        }

        public void OnStarted()
        {
            IsRunning = true;
        }

        public void OnFinished()
        {
            IsRunning = false;
        }
    }
}
