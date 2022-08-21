namespace SlimMessageBus.Host.FluentValidation;

using global::FluentValidation;
using SlimMessageBus;
using SlimMessageBus.Host.Interceptor;

public class ConsumerValidationInterceptor<T> : IConsumerInterceptor<T>
{
    private readonly IValidator<T> _validator;

    public ConsumerValidationInterceptor(IValidator<T> validator) => _validator = validator;

    public async Task OnHandle(T message, CancellationToken cancellationToken, Func<Task> next, IMessageBus bus, string path, IReadOnlyDictionary<string, object> headers, object consumer)
    {
        await _validator.ValidateAndThrowAsync(message, cancellationToken).ConfigureAwait(false);

        await next().ConfigureAwait(false);
    }
}