namespace SlimMessageBus.Host.FluentValidation;

using global::FluentValidation;
using System.Threading;

public abstract class AbstractValidationInterceptor<T>
{
    private readonly IEnumerable<IValidator<T>> _validators;
    private readonly IValidationErrorsHandler _errorsHandler;

    protected AbstractValidationInterceptor(IEnumerable<IValidator<T>> validators, IValidationErrorsHandler errorsHandler = null)
    {
        _validators = validators;
        _errorsHandler = errorsHandler;
    }

    protected async Task OnValidate(T message, CancellationToken cancellationToken)
    {
        var context = new ValidationContext<T>(message);

        var validationTasks = _validators
            .Select(x => x.ValidateAsync(context, cancellationToken));

        await Task.WhenAll(validationTasks);

        var failures = validationTasks
            .SelectMany(x => x.Result.Errors);

        if (failures.Any())
        {
            var ex = _errorsHandler != null
                ? _errorsHandler.OnValidationErrors(failures)
                : new ValidationException(failures);

            if (ex != null)
            {
                // In some conditions the factory might decide to actually swallow the validation issue
                throw ex;
            }
        }
    }
}