namespace SlimMessageBus.Host.FluentValidation;

using global::FluentValidation;
using SlimMessageBus;
using SlimMessageBus.Host.Interceptor;
using System.Threading;

public class ProducerValidationInterceptor<T> : AbstractValidationInterceptor<T>, IProducerInterceptor<T>
{
    public ProducerValidationInterceptor(IEnumerable<IValidator<T>> validators, IValidationErrorsHandler errorsHandler = null)
        : base(validators, errorsHandler)
    {
    }

    public async Task<object> OnHandle(T message, CancellationToken cancellationToken, Func<Task<object>> next, IMessageBus bus, string path, IDictionary<string, object> headers)
    {
        await OnValidate(message, cancellationToken).ConfigureAwait(false);
        return await next().ConfigureAwait(false);
    }
}
