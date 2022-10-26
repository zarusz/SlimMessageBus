namespace SlimMessageBus.Host.Integration.Test;

using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.AzureServiceBus;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Hybrid;
using SlimMessageBus.Host.Interceptor;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.MsDependencyInjection;
using SlimMessageBus.Host.Test.Common;
using Unity;
using Unity.Microsoft.Logging;

public enum DependencyResolverType
{
    MsDependency = 1,
    Autofac = 2,
    Unity = 3,
}

public enum SerializerType
{
    NewtonsoftJson = 1,
    SystemTextJson = 2
}

[Trait("Category", "Integration")]
public class HybridTests : IDisposable
{
    private IDependencyResolver dependencyResolver;

    private readonly XunitLoggerFactory _loggerFactory;
    private readonly ILogger<HybridTests> _logger;
    private readonly IConfigurationRoot _configuration;
    private IDisposable containerDisposable;

    public HybridTests(ITestOutputHelper testOutputHelper)
    {
        _loggerFactory = new XunitLoggerFactory(testOutputHelper);
        _logger = _loggerFactory.CreateLogger<HybridTests>();

        _configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        Secrets.Load(@"..\..\..\..\..\secrets.txt");
    }

    private void SetupBus(
        DependencyResolverType dependencyResolverType,
        SerializerType serializerType,
        Action<IServiceCollection> servicesBuilderForMsDI = null,
        Action<ContainerBuilder> servicesBuilderForAutofacDI = null,
        Action<IUnityContainer> servicesBuilderForUnityDI = null,
        Assembly[] addConsumersFromAssembly = null,
        Assembly[] addInterceptorsFromAssembly = null,
        Assembly[] addConfiguratorsFromAssembly = null)
    {
        if (dependencyResolverType == DependencyResolverType.MsDependency)
        {
            SetupBusForMsDI(serializerType, servicesBuilderForMsDI, addConsumersFromAssembly, addInterceptorsFromAssembly, addConfiguratorsFromAssembly);
        }
        if (dependencyResolverType == DependencyResolverType.Autofac)
        {
            SetupBusForAutofacDI(serializerType, servicesBuilderForAutofacDI, addConsumersFromAssembly, addInterceptorsFromAssembly, addConfiguratorsFromAssembly);
        }
        if (dependencyResolverType == DependencyResolverType.Unity)
        {
            SetupBusForUnityDI(serializerType, servicesBuilderForUnityDI, addConsumersFromAssembly, addInterceptorsFromAssembly, addConfiguratorsFromAssembly);
        }
    }

    private void SetupBusForMsDI(
        SerializerType serializerType,
        Action<IServiceCollection> servicesBuilderForMsDI = null,
        Assembly[] addConsumersFromAssembly = null,
        Assembly[] addInterceptorsFromAssembly = null,
        Assembly[] addConfiguratorsFromAssembly = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(_loggerFactory);

        services.AddSlimMessageBus(
            (mbb, svp) => SetupBus(mbb, serializerType),
            addConsumersFromAssembly: addConsumersFromAssembly,
            addInterceptorsFromAssembly: addInterceptorsFromAssembly,
            addConfiguratorsFromAssembly: addConfiguratorsFromAssembly);

        servicesBuilderForMsDI?.Invoke(services);

        var serviceProvider = services.BuildServiceProvider();

        dependencyResolver = serviceProvider.GetRequiredService<IDependencyResolver>();

        containerDisposable = serviceProvider;
    }

    private void SetupBusForAutofacDI(
        SerializerType serializerType,
        Action<ContainerBuilder> servicesBuilderForAutofacDI = null,
        Assembly[] addConsumersFromAssembly = null,
        Assembly[] addInterceptorsFromAssembly = null,
        Assembly[] addConfiguratorsFromAssembly = null)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(_loggerFactory).As<ILoggerFactory>();
        builder.RegisterModule(new SlimMessageBusModule
        {
            ConfigureBus = (mbb, ctx) => SetupBus(mbb, serializerType),
            AddConsumersFromAssembly = addConsumersFromAssembly,
            AddInterceptorsFromAssembly = addInterceptorsFromAssembly,
            AddConfiguratorsFromAssembly = addConfiguratorsFromAssembly
        });

        servicesBuilderForAutofacDI?.Invoke(builder);

        var container = builder.Build();

        dependencyResolver = container.Resolve<IDependencyResolver>();

        containerDisposable = container;
    }

    private void SetupBusForUnityDI(
        SerializerType serializerType,
        Action<IUnityContainer> servicesBuilderForUnityDI = null,
        Assembly[] addConsumersFromAssembly = null,
        Assembly[] addInterceptorsFromAssembly = null,
        Assembly[] addConfiguratorsFromAssembly = null)
    {
        var container = new UnityContainer();
        container.AddExtension(new LoggingExtension(_loggerFactory));
        container.AddSlimMessageBus(
            (mbb, svp) => SetupBus(mbb, serializerType),
            addConsumersFromAssembly: addConsumersFromAssembly,
            addInterceptorsFromAssembly: addInterceptorsFromAssembly,
            addConfiguratorsFromAssembly: addConfiguratorsFromAssembly);

        servicesBuilderForUnityDI?.Invoke(container);

        dependencyResolver = container.Resolve<IDependencyResolver>();

        containerDisposable = container;
    }

    public class MemoryBusConfigurator : IMessageBusConfigurator
    {
        public void Configure(MessageBusBuilder builder, string busName)
        {
            if (busName != null) return;

            builder.AddChildBus("Memory", (mbb) =>
            {
                mbb.WithProviderMemory()
                   .AutoDeclareFrom(Assembly.GetExecutingAssembly(), consumerTypeFilter: (consumerType) => consumerType.Name.Contains("Internal"));
            });
        }
    }

    public class AzureServiceBusConfigurator : IMessageBusConfigurator
    {
        private readonly IConfiguration _configuration;

        public AzureServiceBusConfigurator(IConfiguration configuration) => _configuration = configuration;

        public void Configure(MessageBusBuilder builder, string busName)
        {
            if (busName != null) return;

            builder.AddChildBus("AzureSB", (mbb) =>
            {
                var topic = "integration-external-message";
                mbb.Produce<ExternalMessage>(x => x.DefaultTopic(topic));
                mbb.Consume<ExternalMessage>(x => x.Topic(topic).SubscriptionName("test").WithConsumer<ExternalMessageConsumer>());
                var connectionString = Secrets.Service.PopulateSecrets(_configuration["Azure:ServiceBus"]);
                mbb.WithProviderServiceBus(new ServiceBusMessageBusSettings(connectionString));
            });
        }
    }

    private void SetupBus(MessageBusBuilder mbb, SerializerType serializerType)
    {
        mbb.WithProviderHybrid();
        mbb.WithSerializer(serializerType switch
        {
            SerializerType.NewtonsoftJson => new Serialization.Json.JsonMessageSerializer(),
            SerializerType.SystemTextJson => new Serialization.SystemTextJson.JsonMessageSerializer(),
            _ => throw new ArgumentOutOfRangeException(nameof(serializerType))
        });
    }

    public record EventMark(Guid CorrelationId, string Name);

    /// <summary>
    /// This test ensures that in a hybris bus setup External (Azure Service Bus) and Internal (Memory) the external message scope is carried over to memory bus, 
    /// and that the interceptors are invoked (and in the correct order).
    /// </summary>
    /// <returns></returns>
    [Theory]
    [InlineData(DependencyResolverType.MsDependency, SerializerType.NewtonsoftJson)]
    [InlineData(DependencyResolverType.MsDependency, SerializerType.SystemTextJson)]
    [InlineData(DependencyResolverType.Autofac, SerializerType.NewtonsoftJson)]
    [InlineData(DependencyResolverType.Unity, SerializerType.NewtonsoftJson)]
    public async Task When_PublishToMemoryBus_Given_InsideConsumerWithMessageScope_Then_MessageScopeIsCarriedOverToMemoryBusConsumer(DependencyResolverType dependencyResolverType, SerializerType serializerType)
    {
        // arrange
        SetupBus(
            serializerType: serializerType,
            dependencyResolverType: dependencyResolverType,
            addConsumersFromAssembly: new[] { typeof(InternalMessageConsumer).Assembly },
            addInterceptorsFromAssembly: new[] { typeof(InternalMessagePublishInterceptor).Assembly },
            addConfiguratorsFromAssembly: new[] { typeof(MemoryBusConfigurator).Assembly },
            servicesBuilderForMsDI: services =>
            {
                // Unit of work should be shared between InternalMessageConsumer and ExternalMessageConsumer.
                // External consumer creates a message scope which continues to itnernal consumer.
                services.AddScoped<UnitOfWork>();

                // This is a singleton that will collect all the events that happened to verify later what actually happened.
                services.AddSingleton<TestEventCollector<EventMark>>();

                services.AddSingleton<IConfiguration>(_configuration);
            },
            servicesBuilderForAutofacDI: builder =>
            {
                // Unit of work should be shared between InternalMessageConsumer and ExternalMessageConsumer.
                // External consumer creates a message scope which continues to itnernal consumer.
                builder.RegisterType<UnitOfWork>().InstancePerLifetimeScope();

                // This is a singleton that will collect all the events that happened to verify later what actually happened.
                builder.RegisterType<TestEventCollector<EventMark>>().SingleInstance();

                builder.RegisterInstance<IConfiguration>(_configuration);
            },
            servicesBuilderForUnityDI: container =>
            {
                // Unit of work should be shared between InternalMessageConsumer and ExternalMessageConsumer.
                // External consumer creates a message scope which continues to itnernal consumer.
                container.RegisterType<UnitOfWork>(TypeLifetime.Scoped);

                // This is a singleton that will collect all the events that happened to verify later what actually happened.
                container.RegisterType<TestEventCollector<EventMark>>(TypeLifetime.Singleton);

                container.RegisterInstance<IConfiguration>(_configuration);
            }
        );

        var bus = (IPublishBus)dependencyResolver.Resolve(typeof(IPublishBus));

        var store = (TestEventCollector<EventMark>)dependencyResolver.Resolve(typeof(TestEventCollector<EventMark>));

        // Eat up all the outstanding message in case the last test left some
        await store.WaitUntilArriving(newMessagesTimeout: 2);

        store.Clear();
        store.Start();

        // act
        await bus.Publish(new ExternalMessage(Guid.NewGuid()));

        // assert
        var expectedStoreCount = 8;

        // wait until arrives
        await store.WaitUntilArriving(newMessagesTimeout: 5, expectedCount: expectedStoreCount);

        var snapshot = store.Snapshot();

        snapshot.Count.Should().Be(expectedStoreCount);
        var grouping = snapshot.GroupBy(x => x.CorrelationId, x => x.Name).ToDictionary(x => x.Key, x => x.ToList());

        // all of the invocations should happen within the context of one unitOfWork = One CorrelationId = One Message Scope
        grouping.Count.Should().Be(2);

        // in this order
        var eventsThatHappenedWhenExternalWasPublished = grouping.Values.SingleOrDefault(x => x.Count == 2);
        eventsThatHappenedWhenExternalWasPublished.Should().NotBeNull();
        eventsThatHappenedWhenExternalWasPublished[0].Should().Be(nameof(ExternalMessageProducerInterceptor));
        eventsThatHappenedWhenExternalWasPublished[1].Should().Be(nameof(ExternalMessagePublishInterceptor));

        // in this order
        var eventsThatHappenedWhenExternalWasConsumed = grouping.Values.SingleOrDefault(x => x.Count == 6);
        eventsThatHappenedWhenExternalWasConsumed.Should().NotBeNull();
        eventsThatHappenedWhenExternalWasConsumed[0].Should().Be(nameof(ExternalMessageConsumerInterceptor));
        eventsThatHappenedWhenExternalWasConsumed[1].Should().Be(nameof(ExternalMessageConsumer));
        eventsThatHappenedWhenExternalWasConsumed[2].Should().Be(nameof(InternalMessageProducerInterceptor));
        eventsThatHappenedWhenExternalWasConsumed[3].Should().Be(nameof(InternalMessagePublishInterceptor));
        eventsThatHappenedWhenExternalWasConsumed[4].Should().Be(nameof(InternalMessageConsumerInterceptor));
        eventsThatHappenedWhenExternalWasConsumed[5].Should().Be(nameof(InternalMessageConsumer));
    }

    public void Dispose()
    {
        if (containerDisposable != null)
        {
            containerDisposable.Dispose();
            containerDisposable = null;
        }
    }

    public class UnitOfWork
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();

        public Task Commit() => Task.CompletedTask;
    }

    public class ExternalMessageConsumer : IConsumer<ExternalMessage>
    {
        private readonly IMessageBus bus;
        private readonly UnitOfWork unitOfWork;
        private readonly TestEventCollector<EventMark> store;

        public ExternalMessageConsumer(IMessageBus bus, UnitOfWork unitOfWork, TestEventCollector<EventMark> store)
        {
            this.bus = bus;
            this.unitOfWork = unitOfWork;
            this.store = store;
        }

        public async Task OnHandle(ExternalMessage message)
        {
            store.Add(new(unitOfWork.CorrelationId, nameof(ExternalMessageConsumer)));

            // ensure the test has started
            if (!store.IsStarted) return;

            // some processing

            await bus.Publish(new InternalMessage(message.CustomerId));

            // some processing

            await unitOfWork.Commit();
        }
    }

    public class InternalMessageConsumer : IConsumer<InternalMessage>
    {
        private readonly UnitOfWork unitOfWork;
        private readonly TestEventCollector<EventMark> store;

        public InternalMessageConsumer(UnitOfWork unitOfWork, TestEventCollector<EventMark> store)
        {
            this.unitOfWork = unitOfWork;
            this.store = store;
        }

        public Task OnHandle(InternalMessage message)
        {
            store.Add(new(unitOfWork.CorrelationId, nameof(InternalMessageConsumer)));

            // some processing

            return Task.CompletedTask;
        }
    }

    public record ExternalMessage(Guid CustomerId);

    public record InternalMessage(Guid CustomerId);

    public class InternalMessageProducerInterceptor : IProducerInterceptor<InternalMessage>
    {
        private readonly UnitOfWork unitOfWork;
        private readonly TestEventCollector<EventMark> store;

        public InternalMessageProducerInterceptor(UnitOfWork unitOfWork, TestEventCollector<EventMark> store)
        {
            this.unitOfWork = unitOfWork;
            this.store = store;
        }

        public Task<object> OnHandle(InternalMessage message, Func<Task<object>> next, IProducerContext context)
        {
            store.Add(new(unitOfWork.CorrelationId, nameof(InternalMessageProducerInterceptor)));

            return next();
        }
    }

    public class InternalMessagePublishInterceptor : IPublishInterceptor<InternalMessage>
    {
        private readonly UnitOfWork unitOfWork;
        private readonly TestEventCollector<EventMark> store;

        public InternalMessagePublishInterceptor(UnitOfWork unitOfWork, TestEventCollector<EventMark> store)
        {
            this.unitOfWork = unitOfWork;
            this.store = store;
        }

        public Task OnHandle(InternalMessage message, Func<Task> next, IProducerContext context)
        {
            store.Add(new(unitOfWork.CorrelationId, nameof(InternalMessagePublishInterceptor)));

            return next();
        }
    }

    public class ExternalMessageProducerInterceptor : IProducerInterceptor<ExternalMessage>
    {
        private readonly UnitOfWork unitOfWork;
        private readonly TestEventCollector<EventMark> store;

        public ExternalMessageProducerInterceptor(UnitOfWork unitOfWork, TestEventCollector<EventMark> store)
        {
            this.unitOfWork = unitOfWork;
            this.store = store;
        }

        public Task<object> OnHandle(ExternalMessage message, Func<Task<object>> next, IProducerContext context)
        {
            store.Add(new(unitOfWork.CorrelationId, nameof(ExternalMessageProducerInterceptor)));

            return next();
        }
    }

    public class ExternalMessagePublishInterceptor : IPublishInterceptor<ExternalMessage>
    {
        private readonly UnitOfWork unitOfWork;
        private readonly TestEventCollector<EventMark> store;

        public ExternalMessagePublishInterceptor(UnitOfWork unitOfWork, TestEventCollector<EventMark> store)
        {
            this.unitOfWork = unitOfWork;
            this.store = store;
        }

        public Task OnHandle(ExternalMessage message, Func<Task> next, IProducerContext context)
        {
            store.Add(new(unitOfWork.CorrelationId, nameof(ExternalMessagePublishInterceptor)));

            return next();
        }
    }

    public class InternalMessageConsumerInterceptor : IConsumerInterceptor<InternalMessage>
    {
        private readonly UnitOfWork unitOfWork;
        private readonly TestEventCollector<EventMark> store;

        public InternalMessageConsumerInterceptor(UnitOfWork unitOfWork, TestEventCollector<EventMark> store)
        {
            this.unitOfWork = unitOfWork;
            this.store = store;
        }

        public Task OnHandle(InternalMessage message, Func<Task> next, IConsumerContext context)
        {
            store.Add(new(unitOfWork.CorrelationId, nameof(InternalMessageConsumerInterceptor)));

            return next();
        }
    }

    public class ExternalMessageConsumerInterceptor : IConsumerInterceptor<ExternalMessage>
    {
        private readonly UnitOfWork unitOfWork;
        private readonly TestEventCollector<EventMark> store;

        public ExternalMessageConsumerInterceptor(UnitOfWork unitOfWork, TestEventCollector<EventMark> store)
        {
            this.unitOfWork = unitOfWork;
            this.store = store;
        }

        public Task OnHandle(ExternalMessage message, Func<Task> next, IConsumerContext context)
        {
            store.Add(new(unitOfWork.CorrelationId, nameof(ExternalMessageConsumerInterceptor)));

            return next();
        }
    }
}
