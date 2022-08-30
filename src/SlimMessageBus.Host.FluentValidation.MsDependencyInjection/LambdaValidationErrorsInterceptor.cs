namespace SlimMessageBus.Host.FluentValidation;

internal class LambdaValidationErrorsInterceptor : IValidationErrorsHandler
{
    private readonly Func<IEnumerable<global::FluentValidation.Results.ValidationFailure>, Exception> _lambda;

    public LambdaValidationErrorsInterceptor(Func<IEnumerable<global::FluentValidation.Results.ValidationFailure>, Exception> lambda) => _lambda = lambda;

    public Exception OnValidationErrors(IEnumerable<global::FluentValidation.Results.ValidationFailure> errors)
        => _lambda(errors);
}
