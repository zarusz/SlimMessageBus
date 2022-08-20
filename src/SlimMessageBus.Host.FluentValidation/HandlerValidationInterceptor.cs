namespace SlimMessageBus.Host.FluentValidation;

using global::FluentValidation;
using SlimMessageBus;
using SlimMessageBus.Host.Interceptor;

public class HandlerValidationInterceptor<T, R> : IRequestHandlerInterceptor<T, R>
{
    private readonly IValidator<T> _validator;

    public HandlerValidationInterceptor(IValidator<T> validator) => _validator = validator;

    public async Task<R> OnHandle(T message, CancellationToken cancellationToken, Func<Task<R>> next, IMessageBus bus, string path, IReadOnlyDictionary<string, object> headers, object handler)
    {
        await _validator.ValidateAndThrowAsync(message).ConfigureAwait(false);

        return await next().ConfigureAwait(false);
    }
}