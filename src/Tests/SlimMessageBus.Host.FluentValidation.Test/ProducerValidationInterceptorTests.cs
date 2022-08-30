namespace SlimMessageBus.Host.FluentValidation.Test;

using global::FluentValidation;
using global::FluentValidation.Results;

public class ProducerValidationInterceptorTests
{
    private readonly Message _message;
    private readonly Mock<IValidator<Message>> _validatorMock;
    private readonly CancellationToken _cancellationToken;
    private readonly ProducerValidationInterceptor<Message> _subject;
    private readonly Mock<Func<Task<object>>> _nextMock;
    private readonly Mock<IMessageBus> _messageBusMock;

    public ProducerValidationInterceptorTests()
    {
        _message = new Message();
        _validatorMock = new Mock<IValidator<Message>>();
        _cancellationToken = new CancellationToken();
        _subject = new ProducerValidationInterceptor<Message>(new[] { _validatorMock.Object }, null);
        _nextMock = new Mock<Func<Task<object>>>();
        _messageBusMock = new Mock<IMessageBus>();
    }

    public record Message;

    [Fact]
    public async Task Given_Validator_When_ValidationFails_Then_RaisesException()
    {
        // arrange
        _validatorMock.Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<Message>>(), _cancellationToken)).ThrowsAsync(new ValidationException("Bad message"));

        // act
        Func<Task<object>> act = () => _subject.OnHandle(_message, _cancellationToken, _nextMock.Object, _messageBusMock.Object, "path", headers: null);

        // asset
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Given_Validator_When_ValidationSucceeds_Then_CallsNext()
    {
        // arrange
        _validatorMock.Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<Message>>(), _cancellationToken)).ReturnsAsync(new ValidationResult());

        // act
        await _subject.OnHandle(_message, _cancellationToken, _nextMock.Object, _messageBusMock.Object, "path", headers: null);

        // asset
        _nextMock.Verify(x => x(), Times.Once);
        _nextMock.VerifyNoOtherCalls();
    }
}
