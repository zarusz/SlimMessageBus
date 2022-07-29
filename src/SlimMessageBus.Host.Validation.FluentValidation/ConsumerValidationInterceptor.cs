namespace SlimMessageBus.Host.Validation.FluentValidation
{
    using global::FluentValidation;
    using SlimMessageBus;
    using SlimMessageBus.Host.Interceptor;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class ConsumerValidationInterceptor<T> : IConsumerInterceptor<T>
    {
        private readonly IValidator<T> validator;

        public ConsumerValidationInterceptor(IValidator<T> validator)
        {
            this.validator = validator;
        }

        public async Task OnHandle(T message, CancellationToken cancellationToken, Func<Task> next, IMessageBus bus, string path, IReadOnlyDictionary<string, object> headers, object consumer)
        {
            await validator.ValidateAndThrowAsync(message).ConfigureAwait(false);

            await next().ConfigureAwait(false);
        }
    }
}