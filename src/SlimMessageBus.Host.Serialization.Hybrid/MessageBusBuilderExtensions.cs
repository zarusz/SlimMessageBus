namespace SlimMessageBus.Host.Serialization.Hybrid;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Builders;

public static class MessageBusBuilderExtensions
{
    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="HybridMessageSerializer"/>.
    /// </summary>
    /// <param name="mbb"></param>
    /// <param name="registration"></param>
    /// <param name="defaultMessageSerializer">The default serializer to be used when the message type cannot be matched</param>
    /// <returns></returns>
    public static MessageBusBuilder AddHybridSerializer(this MessageBusBuilder mbb, IDictionary<IMessageSerializer, Type[]> registration, IMessageSerializer defaultMessageSerializer)
    {
        mbb.PostConfigurationActions.Add(services =>
        {
            services.TryAddSingleton(svp => new HybridMessageSerializer(svp.GetRequiredService<ILogger<HybridMessageSerializer>>(), registration, defaultMessageSerializer));
            services.TryAddSingleton<IMessageSerializer>(svp => svp.GetRequiredService<HybridMessageSerializer>());
        });
        return mbb;
    }

    /// <summary>
    /// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="HybridMessageSerializer"/> using serializers as registered in the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="mbb"><see cref="MessageBusBuilder"/></param>
    /// <param name="registration">Action to register serializers for dependency injection resolution.</param>
    /// <returns><see cref="MessageBusBuilder"/></returns>
    public static MessageBusBuilder AddHybridSerializer(this MessageBusBuilder mbb, Action<IHybridSerializationBuilder> registration)
    {
        var builder = new HybridSerializerOptionsBuilder();
        registration(builder);

        var registrations = builder.Registrations.Where(x => x.Key == builder.DefaultSerializer || x.Value.Types.Any()).ToList();

        foreach (var messageSerializer in registrations)
        {
            mbb.PostConfigurationActions.Add(messageSerializer.Value.Registration);
        }        

        mbb.PostConfigurationActions.Add(services =>
        {
            services.TryAddSingleton(svp =>
            {
                if (services.Count(x => x.ServiceType == typeof(IMessageSerializer)) > 1)
                {
                    throw new NotSupportedException($"Registering instances of {nameof(IMessageSerializer)} with {nameof(IServiceCollection)} outside of {nameof(AddHybridSerializer)} is not supported.");
                }

                var registrationTypes = registrations.ToDictionary(x => (IMessageSerializer)svp.GetRequiredService(x.Key), x => x.Value.Types);
                var defaultMessageSerializer = builder.DefaultSerializer != null ? (IMessageSerializer)svp.GetRequiredService(builder.DefaultSerializer) : null;
                return new HybridMessageSerializer(svp.GetRequiredService<ILogger<HybridMessageSerializer>>(), registrationTypes, defaultMessageSerializer);
            });

            services.AddSingleton<IMessageSerializer>(svp => svp.GetRequiredService<HybridMessageSerializer>());
        });
        return mbb;
    }

    public sealed class HybridSerializerOptionsBuilder : IHybridSerializationBuilder
    {
        internal Type DefaultSerializer { get; set; }
        internal Dictionary<Type, SerializerRegistration> Registrations { get; } = [];

        public IHybridSerializerBuilderOptions RegisterSerializer<TMessageSerializer>(Action<IServiceCollection> services = null) where TMessageSerializer : class, IMessageSerializer
        {
            var key = typeof(TMessageSerializer);
            if (Registrations.TryGetValue(key, out var rawOptions) && rawOptions is SerializerRegistration options)
            {
                options.Registration = services;
                return options;
            }

            options = new SerializerRegistration(this, typeof(TMessageSerializer), services);
            Registrations.Add(key, options);
            return options;
        }

        internal void SetDefault(Type type, bool status)
        {
            if (status)
            {
                DefaultSerializer = type;
                return;
            }

            if (DefaultSerializer == type)
            {
                DefaultSerializer = null;
            }
        }

        internal class SerializerRegistration : IHybridSerializerBuilderOptions
        {
            private readonly HybridSerializerOptionsBuilder _builder;
            private readonly Type _type;

            public SerializerRegistration(HybridSerializerOptionsBuilder builder, Type type, Action<IServiceCollection> registration)
            {
                this._builder = builder;
                this._type = type;
                Registration = registration;
                Types = [];
            }

            internal Action<IServiceCollection> Registration { get; set; }

            internal Type[] Types { get; set; }

            public IHybridSerializationBuilder AsDefault()
            {
                _builder.SetDefault(_type, true);
                Types = [];
                return _builder;
            }

            public IHybridSerializationBuilder For(params Type[] types)
            {
                _builder.SetDefault(_type, false);
                Types = types;
                return _builder;
            }
        }
    }
}
