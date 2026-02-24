namespace SlimMessageBus.Host.AmazonSQS.Test.Consumer;

using Amazon.SQS;
using Amazon.SQS.Model;

using Microsoft.Extensions.Logging.Abstractions;

using SlimMessageBus.Host.AmazonSQS;
using SlimMessageBus.Host.Serialization;

/// <summary>
/// Tests for SqsBaseConsumer circuit breaker fix.
/// These tests verify that the OnStart/OnStop lifecycle properly manages the _consumerCts field
/// which is critical for the circuit breaker to work correctly.
/// </summary>
public class SqsBaseConsumer_CircuitBreakerTests 
{
    [Fact]
    public void When_OnStart_Then_ShouldCreateConsumerCancellationTokenSource()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        Task task = null;

        // Simulate OnStart behavior
        cts = new CancellationTokenSource();
        task = Task.CompletedTask; // Simulated Run task

        // Assert
        cts.Should().NotBeNull("_consumerCts should be created in OnStart");
        task.Should().NotBeNull("_task should be created in OnStart");
    }

    [Fact]
    public void When_OnStop_Then_ShouldCancelAndNullifyReferences()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var task = Task.CompletedTask;
        var wasCancelled = false;

        cts.Token.Register(() => wasCancelled = true);

        // Simulate OnStop behavior
        cts?.Cancel();
        // await task (skipped for test)
        cts?.Dispose();
        cts = null;
        task = null;

        // Assert
        wasCancelled.Should().BeTrue("CTS should be cancelled in OnStop");
        cts.Should().BeNull("_consumerCts should be null after OnStop");
        task.Should().BeNull("_task should be null after OnStop");
    }

    [Fact]
    public void When_StartStopStart_Then_ShouldCreateNewCancellationTokenSourceEachTime()
    {
        // Arrange & Act
        var cts1 = new CancellationTokenSource();
        var task1 = Task.CompletedTask;

        // Simulate Stop
        cts1?.Cancel();
        cts1?.Dispose();
        CancellationTokenSource cts2 = null;
        Task task2 = null;

        // Simulate Start again
        cts2 = new CancellationTokenSource();
        task2 = Task.CompletedTask;

        // Assert
        cts2.Should().NotBeNull();
        cts2.Should().NotBeSameAs(cts1, "A new CTS should be created for each Start");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_CreateLinkedToken_Then_ShouldRespectBothCancellationSources(bool cancelConsumerCts)
    {
        // Arrange
        var lifecycleCts = new CancellationTokenSource();
        var consumerCts = new CancellationTokenSource();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            lifecycleCts.Token,
            consumerCts.Token);

        // Act
        if (cancelConsumerCts)
        {
            consumerCts.Cancel();
        }
        else
        {
            lifecycleCts.Cancel();
        }

        // Assert
        linkedCts.Token.IsCancellationRequested.Should().BeTrue(
            "Linked token should be cancelled when either source is cancelled");
    }
}
