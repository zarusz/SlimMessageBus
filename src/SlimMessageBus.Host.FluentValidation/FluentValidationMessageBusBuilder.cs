namespace SlimMessageBus.Host.FluentValidation;

using System.Reflection;

using global::FluentValidation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Interceptor;

public class FluentValidationMessageBusBuilder
{
    public MessageBusBuilder Builder { get; internal set; }

    public FluentValidationMessageBusBuilder(MessageBusBuilder mbb) => Builder = mbb;

    private static List<Type> GetMessageTypesFromFoundValidatorImplementations(Assembly assembly, Func<Type, bool>? filterPredicate)
    {
        var validatorOpenGenericType = typeof(IValidator<>);

        var messageTypes = ReflectionDiscoveryScanner.From(assembly,
                filter: x => x.InterfaceType.IsGenericType && x.InterfaceType.GetGenericTypeDefinition() == validatorOpenGenericType && (filterPredicate == null || filterPredicate(x.Type)))
            .ProspectTypes
            .Select(x => x.InterfaceType.GetGenericArguments()[0])
            .ToList();

        return messageTypes;
    }

    /// <summary>
    /// Scans the specified assembly containing the specified type for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ProducerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="mbb"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    public FluentValidationMessageBusBuilder AddProducerValidatorsFromAssemblyContaining<T>(Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        => AddProducerValidatorsFromAssembly(typeof(T).Assembly, filterPredicate, lifetime);

    /// <summary>
    /// Scans the specified assembly for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ProducerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <param name="assembly"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    public FluentValidationMessageBusBuilder AddProducerValidatorsFromAssembly(Assembly assembly, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var messageTypes = GetMessageTypesFromFoundValidatorImplementations(assembly, filterPredicate);

        var producerInterceptorOpenGenericType = typeof(IProducerInterceptor<>);
        var implementationProducerInterceptorOpenGenericType = typeof(ProducerValidationInterceptor<>);

        Builder.PostConfigurationActions.Add(services =>
        {
            foreach (var messageType in messageTypes)
            {
                services.TryAdd(new ServiceDescriptor(
                    serviceType: producerInterceptorOpenGenericType.MakeGenericType(messageType),
                    implementationType: implementationProducerInterceptorOpenGenericType.MakeGenericType(messageType),
                    lifetime: lifetime));
            }
        });

        return this;
    }

    /// <summary>
    /// Scans the specified assembly containing the specified type for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ConsumerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    public FluentValidationMessageBusBuilder AddConsumerValidatorsFromAssemblyContaining<T>(Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        => AddConsumerValidatorsFromAssembly(typeof(T).Assembly, filterPredicate, lifetime);

    /// <summary>
    /// Scans the specified assembly for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ProducerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <param name="assembly"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    public FluentValidationMessageBusBuilder AddConsumerValidatorsFromAssembly(Assembly assembly, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var messageTypes = GetMessageTypesFromFoundValidatorImplementations(assembly, filterPredicate);

        var consumerInterceptorOpenGenericType = typeof(IConsumerInterceptor<>);
        var implementationConsumerInterceptorOpenGenericType = typeof(ConsumerValidationInterceptor<>);

        Builder.PostConfigurationActions.Add(services =>
        {
            foreach (var messageType in messageTypes)
            {
                services.TryAdd(new ServiceDescriptor(
                    serviceType: consumerInterceptorOpenGenericType.MakeGenericType(messageType),
                    implementationType: implementationConsumerInterceptorOpenGenericType.MakeGenericType(messageType),
                    lifetime: lifetime));
            }
        });

        return this;
    }

    /// <summary>
    /// Registers an implemention of <see cref="IValidationErrorsHandler"/> that uses the supplied lambda. The scope is singleton.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="validationErrorsHandler"
    /// <returns></returns>
    public FluentValidationMessageBusBuilder AddValidationErrorsHandler(Func<IEnumerable<global::FluentValidation.Results.ValidationFailure>, Exception> validationErrorsHandler)
    {
        if (validationErrorsHandler == null) throw new ArgumentNullException(nameof(validationErrorsHandler));

        Builder.PostConfigurationActions.Add(services =>
        {
            services.TryAddSingleton<IValidationErrorsHandler>(new LambdaValidationErrorsInterceptor(validationErrorsHandler));
        });

        return this;
    }
}
