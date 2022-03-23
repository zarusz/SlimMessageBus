namespace SlimMessageBus.Host.Integration
{
    using Autofac;
    using FluentAssertions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using SecretStore;
    using SlimMessageBus.Host;
    using SlimMessageBus.Host.AzureServiceBus;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.DependencyResolver;
    using SlimMessageBus.Host.Hybrid;
    using SlimMessageBus.Host.Interceptor;
    using SlimMessageBus.Host.Memory;
    using SlimMessageBus.Host.MsDependencyInjection;
    using SlimMessageBus.Host.Serialization.Json;
    using SlimMessageBus.Host.Test.Common;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Unity;
    using Unity.Microsoft.Logging;
    using Xunit;
    using Xunit.Abstractions;

    public enum DependencyResolverType
    {
        MsDependency = 1,
        Autofac = 2,
        Unity = 3,
    }

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
            Action<MessageBusBuilder> memoryBuilder = null,
            Action<MessageBusBuilder> asbBuilder = null,
            Action<IServiceCollection> servicesBuilderForMsDI = null,
            Action<ContainerBuilder> servicesBuilderForAutofacDI = null,
            Action<IUnityContainer> servicesBuilderForUnityDI = null,
            Assembly[] addConsumersFromAssembly = null,
            Assembly[] addInterceptorsFromAssembly = null)
        {
            if (dependencyResolverType == DependencyResolverType.MsDependency)
            {
                SetupBusForMsDI(memoryBuilder, asbBuilder, servicesBuilderForMsDI, addConsumersFromAssembly, addInterceptorsFromAssembly);
            }
            if (dependencyResolverType == DependencyResolverType.Autofac)
            {
                SetupBusForAutofacDI(memoryBuilder, asbBuilder, servicesBuilderForAutofacDI, addConsumersFromAssembly, addInterceptorsFromAssembly);
            }
            if (dependencyResolverType == DependencyResolverType.Unity)
            {
                SetupBusForUnityDI(memoryBuilder, asbBuilder, servicesBuilderForUnityDI, addConsumersFromAssembly, addInterceptorsFromAssembly);
            }
        }

        private void SetupBusForMsDI(
            Action<MessageBusBuilder> memoryBuilder = null,
            Action<MessageBusBuilder> asbBuilder = null,
            Action<IServiceCollection> servicesBuilderForMsDI = null,
            Assembly[] addConsumersFromAssembly = null,
            Assembly[] addInterceptorsFromAssembly = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory>(_loggerFactory);

            services.AddSlimMessageBus((mbb, svp) =>
            {
                SetupBus(memoryBuilder, asbBuilder, mbb);
            },
            addConsumersFromAssembly: addConsumersFromAssembly,
            addInterceptorsFromAssembly: addInterceptorsFromAssembly);

            servicesBuilderForMsDI?.Invoke(services);

            var serviceProvider = services.BuildServiceProvider();

            dependencyResolver = serviceProvider.GetRequiredService<IDependencyResolver>();

            containerDisposable = serviceProvider;
        }

        private void SetupBusForAutofacDI(
            Action<MessageBusBuilder> memoryBuilder = null,
            Action<MessageBusBuilder> asbBuilder = null,
            Action<ContainerBuilder> servicesBuilderForAutofacDI = null,
            Assembly[] addConsumersFromAssembly = null,
            Assembly[] addInterceptorsFromAssembly = null)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(_loggerFactory).As<ILoggerFactory>();
            builder.RegisterModule(new SlimMessageBusModule
            {
                ConfigureBus = (mbb, ctx) =>
                {
                    SetupBus(memoryBuilder, asbBuilder, mbb);
                },
                AddConsumersFromAssembly = addConsumersFromAssembly,
                AddInterceptorsFromAssembly = addInterceptorsFromAssembly
            });

            servicesBuilderForAutofacDI?.Invoke(builder);

            var container = builder.Build();

            dependencyResolver = container.Resolve<IDependencyResolver>();

            containerDisposable = container;
        }

        private void SetupBusForUnityDI(
            Action<MessageBusBuilder> memoryBuilder = null,
            Action<MessageBusBuilder> asbBuilder = null,
            Action<IUnityContainer> servicesBuilderForUnityDI = null,
            Assembly[] addConsumersFromAssembly = null,
            Assembly[] addInterceptorsFromAssembly = null)
        {
            var container = new UnityContainer();
            container.AddExtension(new LoggingExtension(_loggerFactory));
            container.AddSlimMessageBus((mbb, svp) =>
            {
                SetupBus(memoryBuilder, asbBuilder, mbb);
            },
            addConsumersFromAssembly: addConsumersFromAssembly,
            addInterceptorsFromAssembly: addInterceptorsFromAssembly);

            servicesBuilderForUnityDI?.Invoke(container);

            dependencyResolver = container.Resolve<IDependencyResolver>();

            containerDisposable = container;
        }

        private void SetupBus(Action<MessageBusBuilder> memoryBuilder, Action<MessageBusBuilder> asbBuilder, MessageBusBuilder mbb)
        {
            var settings = new HybridMessageBusSettings
            {
                ["Memory"] = (mbb) =>
                {
                    memoryBuilder?.Invoke(mbb);
                    mbb.WithProviderMemory(new MemoryMessageBusSettings());
                },
                ["AzureSB"] = (mbb) =>
                {
                    var connectionString = Secrets.Service.PopulateSecrets(_configuration["Azure:ServiceBus"]);

                    asbBuilder?.Invoke(mbb);
                    mbb.WithProviderServiceBus(new ServiceBusMessageBusSettings(connectionString));
                }
            };

            mbb
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderHybrid(settings);
        }

        public record EventMark(Guid CorrelationId, string Name);

        /// <summary>
        /// This test ensures that in a hybris bus setup External (Azure Service Bus) and Internal (Memory) the external message scope is carried over to memory bus, 
        /// and that the interceptors are invoked (and in the correct order).
        /// </summary>
        /// <returns></returns>
        [Theory]
        [InlineData(DependencyResolverType.MsDependency)]
        [InlineData(DependencyResolverType.Autofac)]
        [InlineData(DependencyResolverType.Unity)]
        public async Task When_PublishToMemoryBus_Given_InsideConsumerWithMessageScope_Then_MessageScopeIsCarriedOverToMemoryBusConsumer(DependencyResolverType dependencyResolverType)
        {
            // arrange
            var topic = "integration-external-message";

            SetupBus(
                dependencyResolverType: dependencyResolverType,
                memoryBuilder: (mbb) =>
                {
                    mbb.Produce<InternalMessage>(x => x.DefaultTopic(x.MessageType.Name));
                    mbb.Consume<InternalMessage>(x => x.Topic(x.MessageType.Name).WithConsumer<InternalMessageConsumer>());
                },
                asbBuilder: (mbb) =>
                {
                    mbb.Produce<ExternalMessage>(x => x.DefaultTopic(topic));
                    mbb.Consume<ExternalMessage>(x => x.Topic(topic).SubscriptionName("test").WithConsumer<ExternalMessageConsumer>());
                },
                addConsumersFromAssembly: new[] { typeof(InternalMessageConsumer).Assembly },
                addInterceptorsFromAssembly: new[] { typeof(InternalMessagePublishInterceptor).Assembly },
                servicesBuilderForMsDI: services =>
                {
                    // Unit of work should be shared between InternalMessageConsumer and ExternalMessageConsumer.
                    // External consumer creates a message scope which continues to itnernal consumer.
                    services.AddScoped<UnitOfWork>();

                    // This is a singleton that will collect all the events that happened to verify later what actually happened.
                    services.AddSingleton<TestEventCollector<EventMark>>();
                },
                servicesBuilderForAutofacDI: builder =>
                {
                    // Unit of work should be shared between InternalMessageConsumer and ExternalMessageConsumer.
                    // External consumer creates a message scope which continues to itnernal consumer.
                    builder.RegisterType<UnitOfWork>().InstancePerLifetimeScope();

                    // This is a singleton that will collect all the events that happened to verify later what actually happened.
                    builder.RegisterType<TestEventCollector<EventMark>>().SingleInstance();
                },
                servicesBuilderForUnityDI: container =>
                {
                    // Unit of work should be shared between InternalMessageConsumer and ExternalMessageConsumer.
                    // External consumer creates a message scope which continues to itnernal consumer.
                    container.RegisterType<UnitOfWork>(TypeLifetime.Scoped);

                    // This is a singleton that will collect all the events that happened to verify later what actually happened.
                    container.RegisterType<TestEventCollector<EventMark>>(TypeLifetime.Singleton);
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

            public async Task OnHandle(ExternalMessage message, string path)
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

            public Task OnHandle(InternalMessage message, string path)
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

            public Task<object> OnHandle(InternalMessage message, CancellationToken cancellationToken, Func<Task<object>> next, IMessageBus bus, string path, IDictionary<string, object> headers)
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

            public Task OnHandle(InternalMessage message, CancellationToken cancellationToken, Func<Task> next, IMessageBus bus, string path, IDictionary<string, object> headers)
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

            public Task<object> OnHandle(ExternalMessage message, CancellationToken cancellationToken, Func<Task<object>> next, IMessageBus bus, string path, IDictionary<string, object> headers)
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

            public Task OnHandle(ExternalMessage message, CancellationToken cancellationToken, Func<Task> next, IMessageBus bus, string path, IDictionary<string, object> headers)
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

            public Task OnHandle(InternalMessage message, CancellationToken cancellationToken, Func<Task> next, IMessageBus bus, string path, IReadOnlyDictionary<string, object> headers, object consumer)
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

            public Task OnHandle(ExternalMessage message, CancellationToken cancellationToken, Func<Task> next, IMessageBus bus, string path, IReadOnlyDictionary<string, object> headers, object consumer)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(ExternalMessageConsumerInterceptor)));

                return next();
            }
        }
    }
}
