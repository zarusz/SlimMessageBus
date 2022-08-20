namespace SlimMessageBus.Host.FluentValidation;

using global::FluentValidation;
using SlimMessageBus;
using SlimMessageBus.Host.Interceptor;

public class ProducerValidationInterceptor<T> : IProducerInterceptor<T>
{
    private readonly IValidator<T> _validator;

    public ProducerValidationInterceptor(IValidator<T> validator) => _validator = validator;

    public async Task<object> OnHandle(T message, CancellationToken cancellationToken, Func<Task<object>> next, IMessageBus bus, string path, IDictionary<string, object> headers)
    {
        await _validator.ValidateAndThrowAsync(message).ConfigureAwait(false);

        return await next().ConfigureAwait(false);
    }
}