namespace SlimMessageBus.Host.Test;

using SlimMessageBus.Host.Test.Common;

public class PendingRequestManagerTest : IDisposable
{
    private readonly PendingRequestManager _subject;

    private readonly Mock<IPendingRequestStore> _store;
    private readonly Mock<Action<object>> _timeoutCallback;
    private readonly TimeSpan _cleanInterval = TimeSpan.FromMilliseconds(50);

    private readonly FakeTimeProvider _timeProvider;

    public PendingRequestManagerTest()
    {
        var timeZero = DateTimeOffset.UtcNow;
        _timeProvider = new FakeTimeProvider(timeZero);

        _store = new Mock<IPendingRequestStore>();
        _timeoutCallback = new Mock<Action<object>>();

        _subject = new PendingRequestManager(_store.Object, _timeProvider, NullLoggerFactory.Instance, _cleanInterval, _timeoutCallback.Object);
    }

    [Fact]
    public void When_NothingExpired_Then_NoActionIsTaken()
    {
        // arrange
        _store.Setup(x => x.FindAllToCancel(_timeProvider.GetUtcNow())).Returns(Array.Empty<PendingRequestState>());

        // act
        _subject.CleanPendingRequests();

        // assert
        _store.Verify(x => x.Remove(It.IsAny<string>()), Times.Never);
        _timeoutCallback.Verify(x => x(It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public void When_RequestExpired_Then_ItIsRemoved()
    {
        // arrange
        var r1 = new PendingRequestState("r1", "request1", typeof(string), typeof(string), _timeProvider.GetUtcNow(), _timeProvider.GetUtcNow().AddSeconds(30), CancellationToken.None);

        _store.Setup(x => x.FindAllToCancel(_timeProvider.GetUtcNow())).Returns(new[] { r1 });
        _store.Setup(x => x.Remove("r1")).Returns(true);

        // act
        _subject.CleanPendingRequests();

        // assert
        _store.Verify(x => x.RemoveAll(It.Is((IEnumerable<string> y) => y.Contains("r1"))), Times.Once);
        _timeoutCallback.Verify(x => x("request1"), Times.Once);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _subject.Dispose();
        }
    }
}
