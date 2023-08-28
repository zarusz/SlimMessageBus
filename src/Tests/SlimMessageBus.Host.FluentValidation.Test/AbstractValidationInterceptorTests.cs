namespace SlimMessageBus.Host.FluentValidation.Test;

using global::FluentValidation;
using global::FluentValidation.Results;

public class AbstractValidationInterceptorTests
{
    private readonly Message _message;
    private readonly Mock<IValidator<Message>> _validatorMock;
    private readonly CancellationToken _cancellationToken;
    private readonly AbstractValidationInterceptor<Message> _subject;

    public AbstractValidationInterceptorTests()
    {
        _message = new Message();
        _validatorMock = new Mock<IValidator<Message>>();
        _cancellationToken = new CancellationToken();
        _subject = new Mock<AbstractValidationInterceptor<Message>>(new[] { _validatorMock.Object }, null) { CallBase = true }.Object;
    }

    public record Message;

    [Fact]
    public async Task When_OnValidate_Given_ValidationFails_Then_RaisesException()
    {
        // arrange
        _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<Message>>(), _cancellationToken))
            .ReturnsAsync(new ValidationResult { Errors = new List<ValidationFailure> { new() { ErrorMessage = "Something is Wrong" } } });

        // act
        Func<Task> act = () => _subject.OnValidate(_message, _cancellationToken);

        // asset
        await act.Should().ThrowAsync<ValidationException>();

        _validatorMock
            .Verify(x => x.ValidateAsync(It.IsAny<ValidationContext<Message>>(), _cancellationToken), Times.Once);
    }

    [Fact]
    public async Task When_OnValidate_Given_ValidationSucceeds_Then_CallsNext()
    {
        // arrange
        _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<Message>>(), _cancellationToken))
            .ReturnsAsync(new ValidationResult());

        // act
        await _subject.OnValidate(_message, _cancellationToken);

        // asset
        _validatorMock
            .Verify(x => x.ValidateAsync(It.IsAny<ValidationContext<Message>>(), _cancellationToken), Times.Once);
    }
}
