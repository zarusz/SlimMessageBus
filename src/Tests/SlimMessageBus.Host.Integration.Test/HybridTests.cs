namespace SlimMessageBus.Host.Integration.Test;

using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.AzureServiceBus;
using SlimMessageBus.Host.Interceptor;
using SlimMessageBus.Host.Memory;

[Trait("Category", "Integration")]
public class HybridTests(ITestOutputHelper output) : BaseIntegrationTest<HybridTests>(output)
{
    public enum SerializerType
    {
        NewtonsoftJson = 1,
        SystemTextJson = 2
    }

    public record RunOptions(SerializerType SerializerType, Action<IServiceCollection> ServicesBuilder);

    protected RunOptions Options { get; set; }

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus(SetupBus);
        Options?.ServicesBuilder?.Invoke(services);
    }

    private void SetupBus(MessageBusBuilder mbb)
    {
        mbb.AddServicesFromAssemblyContaining<InternalMessageConsumer>();

        if (Options.SerializerType == SerializerType.NewtonsoftJson)
        {
            Serialization.Json.SerializationBuilderExtensions.AddJsonSerializer(mbb);
        }
        if (Options.SerializerType == SerializerType.SystemTextJson)
        {
            Serialization.SystemTextJson.SerializationBuilderExtensions.AddJsonSerializer(mbb);
        }

        mbb.AddChildBus("Memory", (mbb) =>
        {
            mbb.WithProviderMemory()
               .AutoDeclareFrom(Assembly.GetExecutingAssembly(),
                    consumerTypeFilter: t => t.Name.Contains("Internal"));
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
                    cfg.ConnectionString = Secrets.Service.PopulateSecrets(Configuration["Azure:ServiceBus"]);
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
        Options = new RunOptions
        (
            SerializerType: serializerType,
            ServicesBuilder: services =>
            {
                // Unit of work should be shared between InternalMessageConsumer and ExternalMessageConsumer.
                // External consumer creates a message scope which continues to internal consumer.
                services.AddScoped<UnitOfWork>();

                // This is a singleton that will collect all the events that happened to verify later what actually happened.
                services.AddSingleton<List<EventMark>>();

                services.AddSingleton<IConfiguration>(Configuration);
            }
        );

        var bus = ServiceProvider.GetRequiredService<IPublishBus>();
        var store = ServiceProvider.GetRequiredService<List<EventMark>>();

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
            .Should().AllSatisfy(x => x.ContextMessageBusType.Should().Be<MemoryMessageBus>());

        // all the external messages should be processed by Azure Service Bus
        store
            .Where(x => x.Name == nameof(ExternalMessageConsumer) || x.Name == nameof(ExternalMessageConsumerInterceptor))
            .Should().AllSatisfy(x => x.ContextMessageBusType.Should().Be<ServiceBusMessageBus>());

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

    public class UnitOfWork
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();

        public Task Commit() => Task.CompletedTask;
    }

    public class ExternalMessageConsumer(IMessageBus bus, UnitOfWork unitOfWork, List<EventMark> store) : IConsumer<ExternalMessage>, IConsumerWithContext
    {
        public IConsumerContext Context { get; set; }

        public async Task OnHandle(ExternalMessage message, CancellationToken cancellationToken)
        {
            lock (store)
            {
                store.Add(new(unitOfWork.CorrelationId, nameof(ExternalMessageConsumer), GetMessageBusTarget(Context)));
            }
            // some processing

            await bus.Publish(new InternalMessage(message.CustomerId), cancellationToken: cancellationToken);

            // some processing

            await unitOfWork.Commit();
        }
    }

    public class InternalMessageConsumer(UnitOfWork unitOfWork, List<EventMark> store) : IConsumer<InternalMessage>, IConsumerWithContext
    {
        public IConsumerContext Context { get; set; }

        public Task OnHandle(InternalMessage message, CancellationToken cancellationToken)
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
