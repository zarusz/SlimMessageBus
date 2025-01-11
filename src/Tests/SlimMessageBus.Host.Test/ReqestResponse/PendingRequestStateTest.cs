namespace SlimMessageBus.Host.Test;

public class PendingRequestStateTest
{
    [Fact]
    public void When_ToString_Then_ReturnsExpectedValue()
    {
        // arrange
        var request = new object();
        var requestId = "r1";
        var requestType = typeof(object);
        var responseType = typeof(object);
        var state = new PendingRequestState(requestId, request, requestType, responseType, DateTimeOffset.Now, DateTimeOffset.Now.AddSeconds(30), CancellationToken.None);

        // act
        var result = state.ToString();

        // assert
        result.Should().StartWith($"Request(Id: {requestId}, RequestType: {requestType}, ResponseType: {responseType}");
    }
}
