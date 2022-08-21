namespace SlimMessageBus.Host.FluentValidation;

using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System;
using global::FluentValidation;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Interceptor;
using System.Linq;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Scans the specified assembly containing the specified type for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ProducerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    public static IServiceCollection AddProducerValidatorsFromAssemblyContaining<T>(this IServiceCollection services, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        => services.AddProducerValidatorsFromAssembly(typeof(T).Assembly, filterPredicate: filterPredicate, lifetime: lifetime);

    /// <summary>
    /// Scans the specified assembly for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ProducerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assembly"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    public static IServiceCollection AddProducerValidatorsFromAssembly(this IServiceCollection services, Assembly assembly, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var messageTypes = GetMessageTypesFromFoundValidatorImplementations(assembly, filterPredicate);

        var producerInterceptorOpenGenericType = typeof(IProducerInterceptor<>);
        var implementationProducerInterceptorOpenGenericType = typeof(ProducerValidationInterceptor<>);

        foreach (var messageType in messageTypes)
        {
            services.Add(new ServiceDescriptor(serviceType: producerInterceptorOpenGenericType.MakeGenericType(messageType),
                implementationType: implementationProducerInterceptorOpenGenericType.MakeGenericType(messageType),
                lifetime: lifetime));
        }

        return services;
    }

    private static List<Type> GetMessageTypesFromFoundValidatorImplementations(Assembly assembly, Func<Type, bool>? filterPredicate)
    {
        var validatorOpenGenericType = typeof(IValidator<>);

        var messageTypes = ReflectionDiscoveryScanner.From(
                assembly,
                filter: x => x.InterfaceType.IsGenericType && x.InterfaceType.GetGenericTypeDefinition() == validatorOpenGenericType && (filterPredicate == null || filterPredicate(x.Type)))
            .ProspectTypes
            .Select(x => x.InterfaceType.GetGenericArguments()[0])
            .ToList();
        return messageTypes;
    }

    /// <summary>
    /// Scans the specified assembly containing the specified type for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ConsumerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    public static IServiceCollection AddConsumerValidatorsFromAssemblyContaining<T>(this IServiceCollection services, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        => services.AddConsumerValidatorsFromAssembly(typeof(T).Assembly, filterPredicate: filterPredicate, lifetime: lifetime);

    /// <summary>
    /// Scans the specified assembly for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ProducerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assembly"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    public static IServiceCollection AddConsumerValidatorsFromAssembly(this IServiceCollection services, Assembly assembly, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var messageTypes = GetMessageTypesFromFoundValidatorImplementations(assembly, filterPredicate);

        var consumerInterceptorOpenGenericType = typeof(IConsumerInterceptor<>);
        var implementationConsumerInterceptorOpenGenericType = typeof(ConsumerValidationInterceptor<>);

        foreach (var messageType in messageTypes)
        {
            services.Add(new ServiceDescriptor(serviceType: consumerInterceptorOpenGenericType.MakeGenericType(messageType),
                implementationType: implementationConsumerInterceptorOpenGenericType.MakeGenericType(messageType),
                lifetime: lifetime));
        }

        return services;
    }
}
