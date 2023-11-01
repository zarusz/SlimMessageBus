namespace SlimMessageBus.Host.FluentValidation;

using global::FluentValidation;

using SlimMessageBus;
using SlimMessageBus.Host.Interceptor;

public class HandlerValidationInterceptor<T, R> : AbstractValidationInterceptor<T>, IRequestHandlerInterceptor<T, R>
{
    public HandlerValidationInterceptor(IEnumerable<IValidator<T>> validators, IValidationErrorsHandler? errorsHandler = null)
        : base(validators, errorsHandler)
    {
    }

    public async Task<R> OnHandle(T request, Func<Task<R>> next, IConsumerContext context)
    {
        await OnValidate(request, context.CancellationToken).ConfigureAwait(false);
        return await next().ConfigureAwait(false);
    }
}