namespace SlimMessageBus.Host.Test;

public class RetryTests
{
    public class WithDelay
    {

        [Fact]
        public async Task ShouldRetryUntilSuccess()
        {
            // Arrange
            var executionCount = 0;
            Func<CancellationToken, Task> operation = (ct) =>
            {
                executionCount++;
                if (executionCount < 3)
                {
                    throw new InvalidOperationException();
                }

                return Task.CompletedTask;
            };

            Func<Exception, int, bool> shouldRetry = (ex, retryCount) => retryCount < 5;
            var delay = TimeSpan.FromMilliseconds(10);

            // Act
            Func<Task> act = async () => await Retry.WithDelay(operation, shouldRetry, delay);

            // Assert
            await act.Should().NotThrowAsync();
            executionCount.Should().Be(3);
        }

        [Fact]
        public async Task ShouldThrowAfterMaxRetries()
        {
            // Arrange
            Func<CancellationToken, Task> operation = (ct) =>
            {
                throw new InvalidOperationException();
            };

            Func<Exception, int, bool> shouldRetry = (ex, retryCount) => retryCount < 2;
            var delay = TimeSpan.FromMilliseconds(10);

            // Act
            Func<Task> act = async () => await Retry.WithDelay(operation, shouldRetry, delay);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public void ShouldThrowArgumentNullException_WhenShouldRetryIsNull()
        {
            // Arrange
            Func<CancellationToken, Task> operation = (ct) => Task.CompletedTask;
            Func<Exception, int, bool> shouldRetry = null;
            var delay = TimeSpan.FromMilliseconds(10);

            // Act
            Func<Task> act = async () => await Retry.WithDelay(operation, shouldRetry, delay);

            // Assert
            act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("shouldRetry");
        }

        [Fact]
        public async Task ShouldThrowTaskCanceledException_WithSameCancellationToken()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var token = cts.Token;

            Func<CancellationToken, Task> operation = async (ct) =>
            {
                await Task.Delay(1000, ct); // Simulate long-running task
            };

            Func<Exception, int, bool> shouldRetry = (ex, retryCount) => true;
            var delay = TimeSpan.FromMilliseconds(10);

            // Act
            var act = async () =>
            {
                cts.CancelAfter(50);
                await Retry.WithDelay(operation, shouldRetry, delay, cancellationToken: token);
            };

            // Assert
            var exception = await act.Should().ThrowAsync<OperationCanceledException>();
            exception.Which.CancellationToken.Should().Be(token);
        }

        [Fact]
        public async Task ShouldInvokeShouldRetry_WhenTaskCanceledExceptionIsFromDifferentToken()
        {
            // Arrange
            var shouldRetryCalled = false;

            Func<CancellationToken, Task> operation = (ct) =>
            {
                throw new TaskCanceledException(); // Simulate cancellation from a different token
            };

            Func<Exception, int, bool> shouldRetry = (ex, retryCount) =>
            {
                shouldRetryCalled = true;
                return false;
            };

            var delay = TimeSpan.FromMilliseconds(10);
            var cts = new CancellationTokenSource();

            // Act
            Func<Task> act = async () => await Retry.WithDelay(operation, shouldRetry, delay, cancellationToken: cts.Token);

            // Assert
            await act.Should().ThrowAsync<TaskCanceledException>();
            shouldRetryCalled.Should().BeTrue();
        }

        [Fact]
        public void ShouldThrowArgumentNullException_WhenOperationIsNull()
        {
            // Arrange
            Func<CancellationToken, Task> operation = null;
            Func<Exception, int, bool> shouldRetry = (ex, retryCount) => true;
            var delay = TimeSpan.FromMilliseconds(10);

            // Act
            Func<Task> act = async () => await Retry.WithDelay(operation, shouldRetry, delay);

            // Assert
            act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("operation");
        }
    }
}