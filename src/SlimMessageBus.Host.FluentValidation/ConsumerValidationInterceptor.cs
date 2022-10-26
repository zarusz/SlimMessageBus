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

    public async Task OnHandle(T message, Func<Task> next, IConsumerContext context)
    {
        await OnValidate(message, context.CancellationToken).ConfigureAwait(false);
        await next().ConfigureAwait(false);
    }
}