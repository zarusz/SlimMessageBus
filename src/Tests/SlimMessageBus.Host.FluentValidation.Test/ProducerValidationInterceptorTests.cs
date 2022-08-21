namespace SlimMessageBus.Host.FluentValidation.Test;

using global::FluentValidation;
using global::FluentValidation.Results;

public class ProducerValidationInterceptorTests
{
    private Message _message;
    private Mock<IValidator<Message>> _validatorMock;
    private CancellationToken _cancellationToken;
    private ProducerValidationInterceptor<Message> _subject;
    private Mock<Func<Task<object>>> _nextMock;
    private Mock<IMessageBus> _messageBusMock;

    public ProducerValidationInterceptorTests()
    {
        _message = new Message();
        _validatorMock = new Mock<IValidator<Message>>();
        _cancellationToken = new CancellationToken();
        _subject = new ProducerValidationInterceptor<Message>(_validatorMock.Object);
        _nextMock = new Mock<Func<Task<object>>>();
        _messageBusMock = new Mock<IMessageBus>();
    }

    public record Message;

    [Fact]
    public async Task Given_Validator_When_ValidationFails_Then_RaisesException()
    {
        // arrange
        _validatorMock.Setup(x => x.ValidateAsync(_message, It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("PropName", "Invalid Value") }));

        // act
        Func<Task<object>> act = () => _subject.OnHandle(_message, _cancellationToken, _nextMock.Object, _messageBusMock.Object, "path", headers: null);

        // asset
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Given_Validator_When_ValidationSucceeds_Then_CallsNext()
    {
        // arrange
        _validatorMock.Setup(x => x.ValidateAsync(_message, _cancellationToken)).ReturnsAsync(new ValidationResult());

        // act
        await _subject.OnHandle(_message, _cancellationToken, _nextMock.Object, _messageBusMock.Object, "path", headers: null);

        // asset
        _nextMock.Verify(x => x(), Times.Once);
        _nextMock.VerifyNoOtherCalls();
    }
}