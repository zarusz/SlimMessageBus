namespace SlimMessageBus.Host.FluentValidation;

using global::FluentValidation;
using SlimMessageBus;
using SlimMessageBus.Host.Interceptor;

public class ConsumerValidationInterceptor<T> : AbstractValidationInterceptor<T>, IConsumerInterceptor<T>
{
    public ConsumerValidationInterceptor(IEnumerable<IValidator<T>> validators, IValidationErrorsHandler errorsHandler = null)
        : base(validators, errorsHandler)
    {
    }

    public async Task<object> OnHandle(T message, Func<Task<object>> next, IConsumerContext context)
    {
        await OnValidate(message, context.CancellationToken).ConfigureAwait(false);
        return await next().ConfigureAwait(false);
    }
}