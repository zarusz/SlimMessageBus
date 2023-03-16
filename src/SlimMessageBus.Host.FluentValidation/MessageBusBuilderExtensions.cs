namespace SlimMessageBus.Host.FluentValidation;

using System.Reflection;

using global::FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host.Config;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder AddFluentValidation(this MessageBusBuilder mbb, Action<FluentValidationMessageBusBuilder> configuration)
    {
        if (configuration is null) throw new ArgumentNullException(nameof(configuration));

        var pluginBuilder = new FluentValidationMessageBusBuilder(mbb);
        configuration(pluginBuilder);

        return mbb;
    }

    /// <summary>
    /// Scans the specified assembly containing the specified type for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ProducerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    [Obsolete("Use the new configuration API: services.AddSlimMessageBus(mbb => mbb.AddFluentValidation(cfg => { })) instead")]
    public static IServiceCollection AddProducerValidatorsFromAssemblyContaining<T>(this IServiceCollection services, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        => services.AddSlimMessageBus(mbb => mbb.AddFluentValidation(cfg => cfg.AddProducerValidatorsFromAssemblyContaining<T>(filterPredicate, lifetime)));

    /// <summary>
    /// Scans the specified assembly for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ProducerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assembly"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    [Obsolete("Use the new configuration API: services.AddSlimMessageBus(mbb => mbb.AddFluentValidation(cfg => { })) instead")]
    public static IServiceCollection AddProducerValidatorsFromAssembly(this IServiceCollection services, Assembly assembly, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        => services.AddSlimMessageBus(mbb => mbb.AddFluentValidation(cfg => cfg.AddProducerValidatorsFromAssembly(assembly, filterPredicate, lifetime)));

    /// <summary>
    /// Scans the specified assembly containing the specified type for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ConsumerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    [Obsolete("Use the new configuration API: services.AddSlimMessageBus(mbb => mbb.AddFluentValidation(cfg => { })) instead")]
    public static IServiceCollection AddConsumerValidatorsFromAssemblyContaining<T>(this IServiceCollection services, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        => services.AddSlimMessageBus(mbb => mbb.AddFluentValidation(cfg => cfg.AddConsumerValidatorsFromAssembly(typeof(T).Assembly, filterPredicate, lifetime)));

    /// <summary>
    /// Scans the specified assembly for FluentValidation's <see cref="IValidator{T}"/> implementations. For the found types T, registers a <see cref="ProducerValidationInterceptor{T}"/>, so that SlimMessageBus can trigger the validation.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assembly"></param>
    /// <param name="filterPredicate"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    [Obsolete("Use the new configuration API: services.AddSlimMessageBus(mbb => mbb.AddFluentValidation(cfg => { })) instead")]
    public static IServiceCollection AddConsumerValidatorsFromAssembly(this IServiceCollection services, Assembly assembly, Func<Type, bool>? filterPredicate = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        => services.AddSlimMessageBus(mbb => mbb.AddFluentValidation(cfg => cfg.AddConsumerValidatorsFromAssembly(assembly, filterPredicate, lifetime)));

    /// <summary>
    /// Registers an implemention of <see cref="IValidationErrorsHandler"/> that uses the supplied lambda. The scope is singleton.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="validationErrorsHandler"
    /// <returns></returns>
    [Obsolete("Use the new configuration API: services.AddSlimMessageBus(mbb => mbb.AddFluentValidation(cfg => { })) instead")]
    public static IServiceCollection AddValidationErrorsHandler(this IServiceCollection services, Func<IEnumerable<global::FluentValidation.Results.ValidationFailure>, Exception> validationErrorsHandler)
        => services.AddSlimMessageBus(mbb => mbb.AddFluentValidation(cfg => cfg.AddValidationErrorsHandler(validationErrorsHandler)));

}
