namespace SlimMessageBus.Host.Validation.FluentValidation
{
    using global::FluentValidation;
    using SlimMessageBus;
    using SlimMessageBus.Host.Interceptor;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class HandlerValidationInterceptor<T, R> : IRequestHandlerInterceptor<T, R>
    {
        private readonly IValidator<T> validator;

        public HandlerValidationInterceptor(IValidator<T> validator)
        {
            this.validator = validator;
        }

        public async Task OnHandle(T message, CancellationToken cancellationToken, Func<Task> next, IMessageBus bus, string path, IReadOnlyDictionary<string, object> headers, object consumer)
        {
        }

        public async Task<R> OnHandle(T request, CancellationToken cancellationToken, Func<Task<R>> next, IMessageBus bus, string path, IReadOnlyDictionary<string, object> headers, object handler)
        {
            await validator.ValidateAndThrowAsync(request).ConfigureAwait(false);

            return await next().ConfigureAwait(false);
        }
    }
}