namespace SlimMessageBus.Host.FluentValidation;

using global::FluentValidation.Results;

public interface IValidationErrorsHandler
{
    Exception OnValidationErrors(IEnumerable<ValidationFailure> errors);
}
