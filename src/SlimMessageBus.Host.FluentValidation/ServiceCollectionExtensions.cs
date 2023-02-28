namespace SlimMessageBus.Host.FluentValidation;

using System.Reflection;

using global::FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Interceptor;

public static class ServiceCollectionExtensions
{
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
    /// <param name="services"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    public static IServiceCollection AddMessageBusProducerValidatorsFromAssemblyContaining<T>(this IServiceCollection services, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        => services.AddMessageBusProducerValidatorsFromAssembly(typeof(T).Assembly, filterPredicate, lifetime);

    /// <summary>
    /// Scans the specified assembly containing the specified type for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ProducerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    [Obsolete("Use the .AddMessageBusProducerValidatorsFromAssemblyContaining<T>() instead")]
    public static IServiceCollection AddProducerValidatorsFromAssemblyContaining<T>(this IServiceCollection services, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        => services.AddMessageBusProducerValidatorsFromAssemblyContaining<T>(filterPredicate, lifetime);

    /// <summary>
    /// Scans the specified assembly for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ProducerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assembly"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    public static IServiceCollection AddMessageBusProducerValidatorsFromAssembly(this IServiceCollection services, Assembly assembly, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var messageTypes = GetMessageTypesFromFoundValidatorImplementations(assembly, filterPredicate);

        var producerInterceptorOpenGenericType = typeof(IProducerInterceptor<>);
        var implementationProducerInterceptorOpenGenericType = typeof(ProducerValidationInterceptor<>);

        foreach (var messageType in messageTypes)
        {
            services.Add(new ServiceDescriptor(
                serviceType: producerInterceptorOpenGenericType.MakeGenericType(messageType),
                implementationType: implementationProducerInterceptorOpenGenericType.MakeGenericType(messageType),
                lifetime: lifetime));
        }

        return services;
    }

    /// <summary>
    /// Scans the specified assembly for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ProducerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assembly"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    [Obsolete("Use the .AddMessageBusProducerValidatorsFromAssembly() instead")]
    public static IServiceCollection AddProducerValidatorsFromAssembly(this IServiceCollection services, Assembly assembly, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        => services.AddMessageBusProducerValidatorsFromAssembly(assembly, filterPredicate, lifetime);

    /// <summary>
    /// Scans the specified assembly containing the specified type for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ConsumerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    public static IServiceCollection AddMessageBusConsumerValidatorsFromAssemblyContaining<T>(this IServiceCollection services, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        => services.AddMessageBusConsumerValidatorsFromAssembly(typeof(T).Assembly, filterPredicate, lifetime);

    /// <summary>
    /// Scans the specified assembly containing the specified type for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ConsumerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    [Obsolete("Use the .AddMessageBusConsumerValidatorsFromAssemblyContaining<T>() instead")]
    public static IServiceCollection AddConsumerValidatorsFromAssemblyContaining<T>(this IServiceCollection services, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        => services.AddMessageBusConsumerValidatorsFromAssembly(typeof(T).Assembly, filterPredicate, lifetime);

    /// <summary>
    /// Scans the specified assembly for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ProducerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assembly"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    public static IServiceCollection AddMessageBusConsumerValidatorsFromAssembly(this IServiceCollection services, Assembly assembly, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var messageTypes = GetMessageTypesFromFoundValidatorImplementations(assembly, filterPredicate);

        var consumerInterceptorOpenGenericType = typeof(IConsumerInterceptor<>);
        var implementationConsumerInterceptorOpenGenericType = typeof(ConsumerValidationInterceptor<>);

        foreach (var messageType in messageTypes)
        {
            services.Add(new ServiceDescriptor(
                serviceType: consumerInterceptorOpenGenericType.MakeGenericType(messageType),
                implementationType: implementationConsumerInterceptorOpenGenericType.MakeGenericType(messageType),
                lifetime: lifetime));
        }

        return services;
    }

    /// <summary>
    /// Scans the specified assembly for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ProducerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assembly"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    [Obsolete("Use the .AddMessageBusConsumerValidatorsFromAssembly() instead")]
    public static IServiceCollection AddConsumerValidatorsFromAssembly(this IServiceCollection services, Assembly assembly, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        => services.AddMessageBusConsumerValidatorsFromAssembly(assembly, filterPredicate, lifetime);

    /// <summary>
    /// Registers an implemention of <see cref="IValidationErrorsHandler"/> that uses the supplied lambda. The scope is singleton.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="validationErrorsHandler"
    /// <returns></returns>
    public static IServiceCollection AddMessageBusValidationErrorsHandler(this IServiceCollection services, Func<IEnumerable<global::FluentValidation.Results.ValidationFailure>, Exception> validationErrorsHandler)
    {
        if (validationErrorsHandler == null) throw new ArgumentNullException(nameof(validationErrorsHandler));

        services.AddSingleton<IValidationErrorsHandler>(new LambdaValidationErrorsInterceptor(validationErrorsHandler));

        return services;
    }

    /// <summary>
    /// Registers an implemention of <see cref="IValidationErrorsHandler"/> that uses the supplied lambda. The scope is singleton.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="validationErrorsHandler"
    /// <returns></returns>
    public static IServiceCollection AddValidationErrorsHandler(this IServiceCollection services, Func<IEnumerable<global::FluentValidation.Results.ValidationFailure>, Exception> validationErrorsHandler)
        => services.AddMessageBusValidationErrorsHandler(validationErrorsHandler);
}
