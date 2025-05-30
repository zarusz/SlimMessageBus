﻿namespace SlimMessageBus.Host.Serialization.Hybrid;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using SlimMessageBus.Host;

public static class SerializationBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="HybridMessageSerializerProvider"/>.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="registration"></param>
    /// <param name="defaultMessageSerializer">The default serializer to be used when the message type cannot be matched</param>
    /// <returns></returns>
    public static TBuilder AddHybridSerializer<TBuilder>(this TBuilder builder, IDictionary<IMessageSerializerProvider, Type[]> registration, IMessageSerializerProvider defaultMessageSerializer)
        where TBuilder : ISerializationBuilder
    {
        builder.RegisterSerializer<HybridMessageSerializerProvider>(services =>
        {
            services.TryAddSingleton(svp => new HybridMessageSerializerProvider(svp.GetRequiredService<ILogger<HybridMessageSerializerProvider>>(), registration, defaultMessageSerializer));
        });
        return builder;
    }

    /// <summary>
    /// Registers the <see cref="IMessageSerializerProvider"/> with implementation as <see cref="HybridMessageSerializerProvider"/> using serializers as registered in the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="mbb"><see cref="MessageBusBuilder"/></param>
    /// <param name="registration">Action to register serializers for dependency injection resolution.</param>
    /// <returns><see cref="MessageBusBuilder"/></returns>
    public static MessageBusBuilder AddHybridSerializer(this MessageBusBuilder mbb, Action<HybridSerializerOptionsBuilder> registration)
    {
        var builder = new HybridSerializerOptionsBuilder();
        registration(builder);

        foreach (var action in builder.GetServiceRegistrations())
        {
            mbb.PostConfigurationActions.Add(action);
        }

        mbb.PostConfigurationActions.Add(services =>
        {
            services.TryAddSingleton(svp =>
            {
                if (services.Count(x => x.ServiceType == typeof(IMessageSerializerProvider)) > 1)
                {
                    throw new NotSupportedException($"Registering instances of {nameof(IMessageSerializerProvider)} outside of {nameof(AddHybridSerializer)} is not supported.");
                }

                var defaultMessageSerializerType = builder.GetDefaultSerializer();
                var defaultMessageSerializer = defaultMessageSerializerType != null ? (IMessageSerializerProvider)svp.GetRequiredService(builder.GetDefaultSerializer()) : null;
                var typeRegistrations = builder.GetTypeRegistrations().ToDictionary(x => (IMessageSerializerProvider)svp.GetRequiredService(x.Key), x => x.Value);
                return new HybridMessageSerializerProvider(svp.GetRequiredService<ILogger<HybridMessageSerializerProvider>>(), typeRegistrations, defaultMessageSerializer);
            });

            services.AddSingleton<IMessageSerializerProvider>(svp => svp.GetRequiredService<HybridMessageSerializerProvider>());
        });
        return mbb;
    }

    public sealed class HybridSerializerOptionsBuilder
    {
        private readonly List<SerializerConfiguration> _configurations = [];

        public ISerializationBuilder AsDefault()
        {
            var configuration = new DefaultSerializerConfiguration();
            _configurations.Add(configuration);
            return configuration;
        }

        public ISerializationBuilder For(params Type[] types)
        {
            var configuration = new ForSerializerConfiguration(types);
            _configurations.Add(configuration);
            return configuration;
        }

        public Type GetDefaultSerializer()
        {
            return _configurations
                .OfType<DefaultSerializerConfiguration>()
                .LastOrDefault(x => x.IsValid)?
                .Type;
        }

        public IReadOnlyList<Action<IServiceCollection>> GetServiceRegistrations()
        {
            return _configurations
                .Where(x => x.IsValid)
                .Select(x => x.Action)
                .ToList();
        }

        public IReadOnlyDictionary<Type, Type[]> GetTypeRegistrations()
        {
            return _configurations
                .OfType<ForSerializerConfiguration>()
                .Where(x => x.IsValid)
                .ToDictionary(x => x.Type, x => x.Types);
        }

        public abstract class SerializerConfiguration : ISerializationBuilder
        {
            public Action<IServiceCollection> Action { get; private set; }
            public bool IsValid => Type != null;
            public Type Type { get; private set; } = null;

            public void RegisterSerializer<TMessageSerializer>(Action<IServiceCollection> services)
                where TMessageSerializer : class, IMessageSerializerProvider
            {
                Type = typeof(TMessageSerializer);
                Action = services;
            }
        }

        public class ForSerializerConfiguration(Type[] types) : SerializerConfiguration
        {
            public Type[] Types { get; } = types;
        }

        public class DefaultSerializerConfiguration : SerializerConfiguration
        {
        }
    }
}
