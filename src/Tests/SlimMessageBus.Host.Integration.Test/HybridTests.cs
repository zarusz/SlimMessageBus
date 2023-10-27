namespace SlimMessageBus.Host.Integration.Test;

using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.AzureServiceBus;
using SlimMessageBus.Host.Interceptor;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Test.Common;
using SlimMessageBus.Host.Test.Common.IntegrationTest;

public enum SerializerType
{
    NewtonsoftJson = 1,
    SystemTextJson = 2
}

[Trait("Category", "Integration")]
public class HybridTests : IDisposable
{
    private IServiceProvider _serviceProvider;

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

    private void SetupBus(SerializerType serializerType, Action<IServiceCollection> servicesBuilder = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(_loggerFactory);
        services.AddTransient(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddSlimMessageBus(mbb => SetupBus(mbb, serializerType));

        servicesBuilder?.Invoke(services);

        _serviceProvider = services.BuildServiceProvider();

        containerDisposable = _serviceProvider as IDisposable;
    }

    private void SetupBus(MessageBusBuilder mbb, SerializerType serializerType)
    {
        mbb.AddServicesFromAssemblyContaining<InternalMessageConsumer>();

        if (serializerType == SerializerType.NewtonsoftJson)
        {
            Serialization.Json.MessageBusBuilderExtensions.AddJsonSerializer(mbb);
        }
        if (serializerType == SerializerType.SystemTextJson)
        {
            Serialization.SystemTextJson.MessageBusBuilderExtensions.AddJsonSerializer(mbb);
        }

        mbb.AddChildBus("Memory", (mbb) =>
        {
            mbb.WithProviderMemory()
               .AutoDeclareFrom(Assembly.GetExecutingAssembly(), consumerTypeFilter: (consumerType) => consumerType.Name.Contains("Internal"));
        });
        mbb.AddChildBus("AzureSB", (mbb) =>
        {
            var topic = "integration-external-message";
            mbb.Produce<ExternalMessage>(x => x.DefaultTopic(topic));
            mbb.Consume<ExternalMessage>(x => x.Topic(topic).SubscriptionName("test").WithConsumer<ExternalMessageConsumer>());
            mbb.WithProviderServiceBus(cfg => cfg.ConnectionString = Secrets.Service.PopulateSecrets(_configuration["Azure:ServiceBus"]));
        });
    }

    public record EventMark(Guid CorrelationId, string Name);

    /// <summary>
    /// This test ensures that in a hybris bus setup External (Azure Service Bus) and Internal (Memory) the external message scope is carried over to memory bus, 
    /// and that the interceptors are invoked (and in the correct order).
    /// </summary>
    /// <returns></returns>
    [Theory]
    [InlineData(SerializerType.NewtonsoftJson)]
    [InlineData(SerializerType.SystemTextJson)]
    public async Task When_PublishToMemoryBus_Given_InsideConsumerWithMessageScope_Then_MessageScopeIsCarriedOverToMemoryBusConsumer(SerializerType serializerType)
    {
        // arrange
        SetupBus(
            serializerType: serializerType,
            servicesBuilder: services =>
            {
                // Unit of work should be shared between InternalMessageConsumer and ExternalMessageConsumer.
                // External consumer creates a message scope which continues to itnernal consumer.
                services.AddScoped<UnitOfWork>();

                // This is a singleton that will collect all the events that happened to verify later what actually happened.
                services.AddSingleton<List<EventMark>>();

                services.AddSingleton<IConfiguration>(_configuration);
            }
        );

        var bus = _serviceProvider.GetRequiredService<IPublishBus>();
        var store = _serviceProvider.GetRequiredService<List<EventMark>>();

        // Eat up all the outstanding message in case the last test left some
        await store.WaitUntilArriving(newMessagesTimeout: 4);
        store.Clear();

        // act
        await bus.Publish(new ExternalMessage(Guid.NewGuid()));

        // assert
        var expectedStoreCount = 8;

        // wait until arrives
        await store.WaitUntilArriving(newMessagesTimeout: 5, expectedCount: expectedStoreCount);

        store.Count.Should().Be(expectedStoreCount);
        var grouping = store.GroupBy(x => x.CorrelationId, x => x.Name).ToDictionary(x => x.Key, x => x.ToList());

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
        GC.SuppressFinalize(this);
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
        private readonly List<EventMark> store;

        public ExternalMessageConsumer(IMessageBus bus, UnitOfWork unitOfWork, List<EventMark> store)
        {
            this.bus = bus;
            this.unitOfWork = unitOfWork;
            this.store = store;
        }

        public async Task OnHandle(ExternalMessage message)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(ExternalMessageConsumer)));
            }
            // some processing

            await bus.Publish(new InternalMessage(message.CustomerId));

            // some processing

            await unitOfWork.Commit();
        }
    }

    public class InternalMessageConsumer : IConsumer<InternalMessage>
    {
        private readonly UnitOfWork unitOfWork;
        private readonly List<EventMark> store;

        public InternalMessageConsumer(UnitOfWork unitOfWork, List<EventMark> store)
        {
            this.unitOfWork = unitOfWork;
            this.store = store;
        }

        public Task OnHandle(InternalMessage message)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(InternalMessageConsumer)));
            }
            // some processing

            return Task.CompletedTask;
        }
    }

    public record ExternalMessage(Guid CustomerId);

    public record InternalMessage(Guid CustomerId);

    public class InternalMessageProducerInterceptor : IProducerInterceptor<InternalMessage>
    {
        private readonly UnitOfWork unitOfWork;
        private readonly List<EventMark> store;

        public InternalMessageProducerInterceptor(UnitOfWork unitOfWork, List<EventMark> store)
        {
            this.unitOfWork = unitOfWork;
            this.store = store;
        }

        public Task<object> OnHandle(InternalMessage message, Func<Task<object>> next, IProducerContext context)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(InternalMessageProducerInterceptor)));
            }
            return next();
        }
    }

    public class InternalMessagePublishInterceptor : IPublishInterceptor<InternalMessage>
    {
        private readonly UnitOfWork unitOfWork;
        private readonly List<EventMark> store;

        public InternalMessagePublishInterceptor(UnitOfWork unitOfWork, List<EventMark> store)
        {
            this.unitOfWork = unitOfWork;
            this.store = store;
        }

        public Task OnHandle(InternalMessage message, Func<Task> next, IProducerContext context)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(InternalMessagePublishInterceptor)));
            }
            return next();
        }
    }

    public class ExternalMessageProducerInterceptor : IProducerInterceptor<ExternalMessage>
    {
        private readonly UnitOfWork unitOfWork;
        private readonly List<EventMark> store;

        public ExternalMessageProducerInterceptor(UnitOfWork unitOfWork, List<EventMark> store)
        {
            this.unitOfWork = unitOfWork;
            this.store = store;
        }

        public Task<object> OnHandle(ExternalMessage message, Func<Task<object>> next, IProducerContext context)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(ExternalMessageProducerInterceptor)));
            }

            return next();
        }
    }

    public class ExternalMessagePublishInterceptor : IPublishInterceptor<ExternalMessage>
    {
        private readonly UnitOfWork unitOfWork;
        private readonly List<EventMark> store;

        public ExternalMessagePublishInterceptor(UnitOfWork unitOfWork, List<EventMark> store)
        {
            this.unitOfWork = unitOfWork;
            this.store = store;
        }

        public Task OnHandle(ExternalMessage message, Func<Task> next, IProducerContext context)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(ExternalMessagePublishInterceptor)));
            }

            return next();
        }
    }

    public class InternalMessageConsumerInterceptor : IConsumerInterceptor<InternalMessage>
    {
        private readonly UnitOfWork unitOfWork;
        private readonly List<EventMark> store;

        public InternalMessageConsumerInterceptor(UnitOfWork unitOfWork, List<EventMark> store)
        {
            this.unitOfWork = unitOfWork;
            this.store = store;
        }

        public Task<object> OnHandle(InternalMessage message, Func<Task<object>> next, IConsumerContext context)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(InternalMessageConsumerInterceptor)));
            }
            return next();
        }
    }

    public class ExternalMessageConsumerInterceptor : IConsumerInterceptor<ExternalMessage>
    {
        private readonly UnitOfWork unitOfWork;
        private readonly List<EventMark> store;

        public ExternalMessageConsumerInterceptor(UnitOfWork unitOfWork, List<EventMark> store)
        {
            this.unitOfWork = unitOfWork;
            this.store = store;
        }

        public Task<object> OnHandle(ExternalMessage message, Func<Task<object>> next, IConsumerContext context)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(ExternalMessageConsumerInterceptor)));
            }
            return next();
        }
    }
}
