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

    public async Task OnHandle(T message, CancellationToken cancellationToken, Func<Task> next, IMessageBus bus, string path, IReadOnlyDictionary<string, object> headers, object consumer)
    {
        await OnValidate(message, cancellationToken).ConfigureAwait(false);
        await next().ConfigureAwait(false);
    }
}