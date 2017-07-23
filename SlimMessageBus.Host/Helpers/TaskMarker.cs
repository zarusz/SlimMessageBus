using System.Threading;
using System.Threading.Tasks;

namespace SlimMessageBus.Host
{
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
                    Thread.Sleep(100);
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
