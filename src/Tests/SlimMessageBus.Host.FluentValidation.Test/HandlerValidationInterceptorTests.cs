namespace SlimMessageBus.Host.FluentValidation.Test;

using global::FluentValidation;
using global::FluentValidation.Results;

public class HandlerValidationInterceptorTests
{
    private readonly Message _message;
    private readonly Mock<IValidator<Message>> _validatorMock;
    private readonly CancellationToken _cancellationToken;
    private readonly HandlerValidationInterceptor<Message, ResponseMessage> _subject;
    private readonly Mock<IConsumerContext> _consumerContextMock;
    private readonly Mock<Func<Task<ResponseMessage>>> _nextMock;

    public HandlerValidationInterceptorTests()
    {
        _message = new();
        _validatorMock = new();
        _cancellationToken = new();
        _subject = new(new[] { _validatorMock.Object }, null);
        _consumerContextMock = new();
        _nextMock = new();
    }

    public record Message : IRequest<ResponseMessage>;
    public record ResponseMessage;

    [Fact]
    public async Task Given_Validator_When_ValidationFails_Then_RaisesException()
    {
        // arrange
        _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<Message>>(), _cancellationToken))
            .ThrowsAsync(new ValidationException("Bad message"));

        // act
        Func<Task> act = () => _subject.OnHandle(_message, _nextMock.Object, _consumerContextMock.Object);

        // asset
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Given_Validator_When_ValidationSucceeds_Then_CallsNext()
    {
        // arrange
        _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<Message>>(), _cancellationToken))
            .ReturnsAsync(new ValidationResult());

        // act
        await _subject.OnHandle(_message, _nextMock.Object, _consumerContextMock.Object);

        // asset
        _nextMock.Verify(x => x(), Times.Once);
        _nextMock.VerifyNoOtherCalls();
    }
}