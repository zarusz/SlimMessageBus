using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using SlimMessageBus.Host.RequestResponse;
using Xunit;

namespace SlimMessageBus.Host.Test
{
    public class PendingRequestManagerTest : IDisposable
    {
        private readonly PendingRequestManager _subject;

        private readonly Mock<IPendingRequestStore> _store;
        private readonly Mock<Action<object>> _timeoutCallback;
        private readonly TimeSpan _cleanInterval = TimeSpan.FromMilliseconds(50);

        private readonly DateTimeOffset _timeZero;
        private DateTimeOffset _timeNow;

        public PendingRequestManagerTest()
        {
            _timeZero = DateTimeOffset.Now;
            _timeNow = _timeZero;

            _store = new Mock<IPendingRequestStore>();
            _timeoutCallback = new Mock<Action<object>>();

            _subject = new PendingRequestManager(_store.Object, () => _timeNow, _cleanInterval, _timeoutCallback.Object);
        }

        public void Dispose()
        {
        }

        [Fact]
        public void WhenNothingExpired_NoActionIsTaken()
        {
            // arrange
            _store.Setup(x => x.FindAllToCancel(_timeNow)).Returns(new PendingRequestState[0]);

            // act
            _subject.CleanPendingRequests();

            // assert
            _store.Verify(x => x.Remove(It.IsAny<string>()), Times.Never);
            _timeoutCallback.Verify(x => x(It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public void WhenRequestExpired_ItIsRemoved()
        {
            // arrange
            var r1 = new PendingRequestState("r1", "request1", typeof(string), typeof(string), _timeNow, _timeNow.AddSeconds(30), CancellationToken.None);

            _store.Setup(x => x.FindAllToCancel(_timeNow)).Returns(new[] { r1 });
            _store.Setup(x => x.Remove("r1")).Returns(true);

            // act
            _subject.CleanPendingRequests();

            // assert
            _store.Verify(x => x.Remove("r1"), Times.Once);
            _timeoutCallback.Verify(x => x("request1"), Times.Once);
        }
    }
}
