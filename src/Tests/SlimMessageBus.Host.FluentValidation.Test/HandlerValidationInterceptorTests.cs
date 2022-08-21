namespace SlimMessageBus.Host.FluentValidation.Test;

using global::FluentValidation;
using global::FluentValidation.Results;

public class HandlerValidationInterceptorTests
{
    private readonly Message _message;
    private readonly Mock<IValidator<Message>> _validatorMock;
    private readonly CancellationToken _cancellationToken;
    private readonly HandlerValidationInterceptor<Message, ResponseMessage> _subject;
    private readonly Mock<IConsumer<Message>> _consumerMock;
    private readonly Mock<Func<Task<ResponseMessage>>> _nextMock;
    private readonly Mock<IMessageBus> _messageBusMock;

    public HandlerValidationInterceptorTests()
    {
        _message = new Message();
        _validatorMock = new Mock<IValidator<Message>>();
        _cancellationToken = new CancellationToken();
        _subject = new HandlerValidationInterceptor<Message, ResponseMessage>(_validatorMock.Object);
        _consumerMock = new Mock<IConsumer<Message>>();
        _nextMock = new Mock<Func<Task<ResponseMessage>>>();
        _messageBusMock = new Mock<IMessageBus>();
    }

    public record Message : IRequestMessage<ResponseMessage>;
    public record ResponseMessage;

    [Fact]
    public async Task Given_Validator_When_ValidationFails_Then_RaisesException()
    {
        // arrange
        _validatorMock.Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<Message>>(), _cancellationToken)).ThrowsAsync(new ValidationException("Bad message"));

        // act
        Func<Task> act = () => _subject.OnHandle(_message, _cancellationToken, _nextMock.Object, _messageBusMock.Object, "path", headers: null, _consumerMock.Object);

        // asset
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Given_Validator_When_ValidationSucceeds_Then_CallsNext()
    {
        // arrange
        _validatorMock.Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<Message>>(), _cancellationToken)).ReturnsAsync(new ValidationResult());

        // act
        await _subject.OnHandle(_message, _cancellationToken, _nextMock.Object, _messageBusMock.Object, "path", headers: null, _consumerMock.Object);

        // asset
        _nextMock.Verify(x => x(), Times.Once);
        _nextMock.VerifyNoOtherCalls();
    }
}