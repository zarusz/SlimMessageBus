namespace SlimMessageBus.Host.Outbox.Test;

public class OutboxLockRenewalTimerTests
{
    private readonly Mock<ILogger<OutboxLockRenewalTimer>> _loggerMock;
    private readonly Mock<IOutboxRepository> _outboxRepositoryMock;
    private readonly Mock<IInstanceIdProvider> _instanceIdProviderMock;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Action<Exception> _lockLostAction;
    private readonly TimeSpan _lockDuration;
    private readonly TimeSpan _lockRenewalInterval;
    private readonly string _instanceId;

    public OutboxLockRenewalTimerTests()
    {
        _loggerMock = new Mock<ILogger<OutboxLockRenewalTimer>>();
        _outboxRepositoryMock = new Mock<IOutboxRepository>();
        _instanceIdProviderMock = new Mock<IInstanceIdProvider>();
        _cancellationTokenSource = new CancellationTokenSource();
        _lockLostAction = Mock.Of<Action<Exception>>();
        _lockDuration = TimeSpan.FromSeconds(10);
        _lockRenewalInterval = TimeSpan.FromSeconds(9);
        _instanceId = "test-instance-id";

        _instanceIdProviderMock.Setup(p => p.GetInstanceId()).Returns(_instanceId);
    }

    [Fact]
    public void Start_ShouldSetActiveToTrue()
    {
        // Arrange
        var timer = CreateTimer();

        // Act
        timer.Start();

        // Assert
        timer.Active.Should().BeTrue();
    }

    [Fact]
    public void Stop_ShouldSetActiveToFalse()
    {
        // Arrange
        var timer = CreateTimer();
        timer.Start();

        // Act
        timer.Stop();

        // Assert
        timer.Active.Should().BeFalse();
    }

    [Fact]
    public void Dispose_ShouldStopTimer()
    {
        // Arrange
        var timer = CreateTimer();
        timer.Start();

        // Act
        timer.Dispose();

        // Assert
        timer.Active.Should().BeFalse();
    }

    [Fact]
    public async Task CallbackAsync_ShouldInvokeLockLostActionIfLockCannotBeRenewed()
    {
        // Arrange
        _outboxRepositoryMock.Setup(r => r.RenewLock(_instanceId, _lockDuration, _cancellationTokenSource.Token))
                             .ReturnsAsync(false);

        var lockLostActionMock = new Mock<Action<Exception>>();
        var timer = CreateTimer(lockLostAction: lockLostActionMock.Object);
        timer.Start();

        // Act
        await InvokeCallbackAsync(timer);

        // Assert
        lockLostActionMock.Verify(a => a(It.IsAny<Exception>()), Times.Once);
    }

    private OutboxLockRenewalTimer CreateTimer(Action<Exception> lockLostAction = null)
    {
        return new OutboxLockRenewalTimer(
            _loggerMock.Object,
            _outboxRepositoryMock.Object,
            _instanceIdProviderMock.Object,
            _lockDuration,
            _lockRenewalInterval,
            _cancellationTokenSource.Token,
            lockLostAction ?? _lockLostAction);
    }

    private async Task InvokeCallbackAsync(OutboxLockRenewalTimer timer)
    {
        var callbackMethod = typeof(OutboxLockRenewalTimer).GetMethod("CallbackAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)callbackMethod.Invoke(timer, null);
    }
}
