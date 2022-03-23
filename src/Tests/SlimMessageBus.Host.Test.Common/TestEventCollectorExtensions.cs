namespace SlimMessageBus.Host.Test.Common
{
    using System.Diagnostics;
    using System.Threading.Tasks;

    public static class TestEventCollectorExtensions
    {
        public static async Task WaitUntilArriving<T>(this TestEventCollector<T> collector, int newMessagesTimeout = 5, int? expectedCount = null)
        {
            var lastMessageCount = 0;
            var lastMessageStopwatch = Stopwatch.StartNew();

            while (lastMessageStopwatch.Elapsed.TotalSeconds < newMessagesTimeout && (expectedCount == null || expectedCount.Value != collector.Count))
            {
                await Task.Delay(250).ConfigureAwait(false);

                var count = collector.Count;
                if (count != lastMessageCount)
                {
                    lastMessageCount = count;
                    lastMessageStopwatch.Restart();
                }
            }
            lastMessageStopwatch.Stop();
        }
    }
}