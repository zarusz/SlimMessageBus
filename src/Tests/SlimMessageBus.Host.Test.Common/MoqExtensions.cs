namespace SlimMessageBus.Host.Test.Common;

using System.Diagnostics;
using System.Linq.Expressions;

public static class MoqExtensions
{
    public static async Task VerifyWithRetry<T, TResult>(this Mock<T> mock, TimeSpan timeout, Expression<Func<T, TResult>> expression, Times times)
        where T : class
    {
        var stopwatch = Stopwatch.StartNew();
        do
        {
            try
            {
                mock.Verify(expression, times);
                return;
            }
            catch (MockException)
            {
                if (stopwatch.Elapsed > timeout)
                {
                    // when timed out rethrow the exception
                    throw;
                }
                // else keep on repeating
                await Task.Delay(50);
            }
        } while (true);
    }
}
