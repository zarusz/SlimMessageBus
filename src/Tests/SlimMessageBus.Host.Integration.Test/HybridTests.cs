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
            Serialization.Json.SerializationBuilderExtensions.AddJsonSerializer(mbb);
        }
        if (serializerType == SerializerType.SystemTextJson)
        {
            Serialization.SystemTextJson.SerializationBuilderExtensions.AddJsonSerializer(mbb);
        }

        mbb.AddChildBus("Memory", (mbb) =>
        {
            mbb.WithProviderMemory()
               .AutoDeclareFrom(Assembly.GetExecutingAssembly(), consumerTypeFilter: (consumerType) => consumerType.Name.Contains("Internal"));
        });
        mbb.AddChildBus("AzureSB", (mbb) =>
        {
            var topic = "integration-external-message";
            mbb
                .Produce<ExternalMessage>(x => x.DefaultTopic(topic))
                .Consume<ExternalMessage>(x => x.Topic(topic).Instances(20))
                .WithProviderServiceBus(cfg =>
                {
                    cfg.SubscriptionName("test");
                    cfg.ConnectionString = Secrets.Service.PopulateSecrets(_configuration["Azure:ServiceBus"]);
                    cfg.PrefetchCount = 100;
                });
        });
    }

    public record EventMark(Guid CorrelationId, string Name, Type ContextMessageBusType);

    /// <summary>
    /// This test ensures that in a hybrid bus setup External (Azure Service Bus) and Internal (Memory) the external message scope is carried over to memory bus, 
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
                // External consumer creates a message scope which continues to internal consumer.
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


        // all the internal messages should be processed by Memory bus
        store
            .Where(x => x.Name == nameof(InternalMessageConsumer) || x.Name == nameof(InternalMessageConsumerInterceptor) || x.Name == nameof(InternalMessageProducerInterceptor) || x.Name == nameof(InternalMessagePublishInterceptor))
            .Should().AllSatisfy(x => x.ContextMessageBusType.Should().Be(typeof(MemoryMessageBus)));

        // all the external messages should be processed by Azure Service Bus
        store
            .Where(x => x.Name == nameof(ExternalMessageConsumer) || x.Name == nameof(ExternalMessageConsumerInterceptor))
            .Should().AllSatisfy(x => x.ContextMessageBusType.Should().Be(typeof(ServiceBusMessageBus)));

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

    public class ExternalMessageConsumer(IMessageBus bus, UnitOfWork unitOfWork, List<EventMark> store) : IConsumer<ExternalMessage>, IConsumerWithContext
    {
        public IConsumerContext Context { get; set; }

        public async Task OnHandle(ExternalMessage message)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(ExternalMessageConsumer), GetMessageBusTarget(Context)));
            }
            // some processing

            await bus.Publish(new InternalMessage(message.CustomerId));

            // some processing

            await unitOfWork.Commit();
        }
    }

    public class InternalMessageConsumer(UnitOfWork unitOfWork, List<EventMark> store) : IConsumer<InternalMessage>, IConsumerWithContext
    {
        public IConsumerContext Context { get; set; }

        public Task OnHandle(InternalMessage message)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(InternalMessageConsumer), GetMessageBusTarget(Context)));
            }
            // some processing
            return Task.CompletedTask;
        }
    }

    public record ExternalMessage(Guid CustomerId);

    public record InternalMessage(Guid CustomerId);

    public class InternalMessageProducerInterceptor(UnitOfWork unitOfWork, List<EventMark> store) : IProducerInterceptor<InternalMessage>
    {
        public Task<object> OnHandle(InternalMessage message, Func<Task<object>> next, IProducerContext context)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(InternalMessageProducerInterceptor), GetMessageBusTarget(context)));
            }
            return next();
        }
    }

    private static Type GetMessageBusTarget(IConsumerContext context)
    {
        var messageBusTarget = context.Bus is IMessageBusTarget busTarget
            ? busTarget.Target
            : null;

        return messageBusTarget?.GetType();
    }

    private static Type GetMessageBusTarget(IProducerContext context)
    {
        var messageBusTarget = context.Bus is IMessageBusTarget busTarget
            ? busTarget.Target
            : null;

        return messageBusTarget?.GetType();
    }

    public class InternalMessagePublishInterceptor(UnitOfWork unitOfWork, List<EventMark> store) : IPublishInterceptor<InternalMessage>
    {
        public Task OnHandle(InternalMessage message, Func<Task> next, IProducerContext context)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(InternalMessagePublishInterceptor), GetMessageBusTarget(context)));
            }
            return next();
        }
    }

    public class ExternalMessageProducerInterceptor(UnitOfWork unitOfWork, List<EventMark> store) : IProducerInterceptor<ExternalMessage>
    {
        public Task<object> OnHandle(ExternalMessage message, Func<Task<object>> next, IProducerContext context)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(ExternalMessageProducerInterceptor), GetMessageBusTarget(context)));
            }
            return next();
        }
    }

    public class ExternalMessagePublishInterceptor(UnitOfWork unitOfWork, List<EventMark> store) : IPublishInterceptor<ExternalMessage>
    {
        public Task OnHandle(ExternalMessage message, Func<Task> next, IProducerContext context)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(ExternalMessagePublishInterceptor), GetMessageBusTarget(context)));
            }

            return next();
        }
    }

    public class InternalMessageConsumerInterceptor(UnitOfWork unitOfWork, List<EventMark> store) : IConsumerInterceptor<InternalMessage>
    {
        public Task<object> OnHandle(InternalMessage message, Func<Task<object>> next, IConsumerContext context)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(InternalMessageConsumerInterceptor), GetMessageBusTarget(context)));
            }
            return next();
        }
    }

    public class ExternalMessageConsumerInterceptor(UnitOfWork unitOfWork, List<EventMark> store) : IConsumerInterceptor<ExternalMessage>
    {
        public Task<object> OnHandle(ExternalMessage message, Func<Task<object>> next, IConsumerContext context)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(ExternalMessageConsumerInterceptor), GetMessageBusTarget(context)));
            }
            return next();
        }
    }
}
