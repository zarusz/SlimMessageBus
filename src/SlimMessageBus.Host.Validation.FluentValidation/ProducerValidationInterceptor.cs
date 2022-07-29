namespace SlimMessageBus.Host.Validation.FluentValidation
{
    using global::FluentValidation;
    using SlimMessageBus;
    using SlimMessageBus.Host.Interceptor;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class ProducerValidationInterceptor<T> : IProducerInterceptor<T>
    {
        private readonly IValidator<T> validator;

        public ProducerValidationInterceptor(IValidator<T> validator)
        {
            this.validator = validator;
        }

        public async Task<object> OnHandle(T message, CancellationToken cancellationToken, Func<Task<object>> next, IMessageBus bus, string path, IDictionary<string, object> headers)
        {
            await validator.ValidateAndThrowAsync(message).ConfigureAwait(false);

            return await next().ConfigureAwait(false);
        }
    }
}