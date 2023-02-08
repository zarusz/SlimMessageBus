namespace SlimMessageBus.Host.Test.Common.IntegrationTest;

using System.Diagnostics;

public static class TestEventCollectorExtensions
{
    public static Task WaitUntilArriving<T>(this TestEventCollector<T> collection, int newMessagesTimeout = 5, int? expectedCount = null) =>
        WaitUntilArriving(() => collection.Count, newMessagesTimeout, expectedCount);

    public static Task WaitUntilArriving<T>(this IReadOnlyCollection<T> collection, int newMessagesTimeout = 5, int? expectedCount = null) =>
        WaitUntilArriving(() => collection.Count, newMessagesTimeout, expectedCount);

    public static async Task WaitUntilArriving(Func<int> countSelector, int newMessagesTimeout = 5, int? expectedCount = null)
    {
        var lastMessageCount = 0;
        var lastMessageStopwatch = Stopwatch.StartNew();

        while (lastMessageStopwatch.Elapsed.TotalSeconds < newMessagesTimeout && (expectedCount == null || expectedCount.Value != countSelector()))
        {
            await Task.Delay(250).ConfigureAwait(false);

            var count = countSelector();
            if (count != lastMessageCount)
            {
                lastMessageCount = count;
                lastMessageStopwatch.Restart();
            }
        }
        lastMessageStopwatch.Stop();
    }

}