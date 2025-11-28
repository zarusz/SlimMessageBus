namespace SlimMessageBus.Host.Test;

using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Hybrid;
using SlimMessageBus.Host.Test.Common;

public class MessageBusBaseTests : IDisposable
{
    private MessageBusBuilder BusBuilder { get; set; }
    private readonly Lazy<MessageBusTested> _busLazy;
    private MessageBusTested Bus => _busLazy.Value;
    private readonly DateTimeOffset _timeZero;
    private FakeTimeProvider _timeProvider;

    private const int TimeoutFor5 = 5;
    private const int TimeoutDefault10 = 10;
    private readonly Mock<IServiceProvider> _serviceProviderMock;

    public IList<ProducedMessage> _producedMessages;

    public record ProducedMessage(Type MessageType, string Path, object Message);

    public MessageBusBaseTests()
    {
        _timeZero = DateTimeOffset.Now;
        _timeProvider = new FakeTimeProvider(_timeZero);

        _producedMessages = [];

        _serviceProviderMock = new Mock<IServiceProvider>();
        _serviceProviderMock.Setup(x => x.GetService(typeof(IMessageSerializerProvider))).Returns(new JsonMessageSerializer());
        _serviceProviderMock.Setup(x => x.GetService(typeof(IMessageTypeResolver))).Returns(new AssemblyQualifiedNameMessageTypeResolver([]));
        _serviceProviderMock.Setup(x => x.GetService(typeof(TimeProvider))).Returns(() => _timeProvider);
        _serviceProviderMock.Setup(x => x.GetService(It.Is<Type>(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)))).Returns((Type t) => Array.CreateInstance(t.GetGenericArguments()[0], 0));
        _serviceProviderMock.Setup(x => x.GetService(typeof(RuntimeTypeCache))).Returns(new RuntimeTypeCache());
        _serviceProviderMock.Setup(x => x.GetService(typeof(IPendingRequestManager))).Returns(() => new PendingRequestManager(new InMemoryPendingRequestStore(), _timeProvider, NullLoggerFactory.Instance));

        BusBuilder = MessageBusBuilder.Create()
            .Produce<RequestA>(x =>
            {
                x.DefaultTopic("a-requests");
                x.DefaultTimeout(TimeSpan.FromSeconds(TimeoutFor5));
            })
            .Produce<RequestB>(x =>
            {
                x.DefaultTopic("b-requests");
            })
            .ExpectRequestResponses(x =>
            {
                x.ReplyToTopic("app01-responses");
                x.DefaultTimeout(TimeSpan.FromSeconds(TimeoutDefault10));
            })
            .WithServiceProvider(_serviceProviderMock.Object)
            .WithProvider(s =>
            {
                return new MessageBusTested(s, _timeProvider)
                {
                    // provide current time
                    OnProduced = (mt, n, m) => _producedMessages.Add(new(mt, n, m))
                };
            });

        _busLazy = new Lazy<MessageBusTested>(CreateMessageBus<MessageBusTested>);
    }

    private T CreateMessageBus<T>() where T : IMasterMessageBus
    {
        var bus = (T)BusBuilder.Build();
        _ = Task.Run(() => bus.AutoStart(default));
        return bus;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_busLazy.IsValueCreated)
            {
                _busLazy.Value.Dispose();
            }
        }
    }

    [Fact]
    public void When_Create_Given_ConfigurationThatDeclaresSameMessageTypeMoreThanOnce_Then_ExceptionIsThrown()
    {
        // arrange
        BusBuilder.Produce<RequestA>(x => x.DefaultTopic("default-topic"));
        BusBuilder.Produce<RequestA>(x => x.DefaultTopic("default-topic-2"));

        // act
        Action busCreation = () => BusBuilder.Build();

        // assert
        busCreation.Should().Throw<ConfigurationMessageBusException>()
            .WithMessage("*was declared more than once*");
    }

    [Theory]
    [InlineData(null, null, true, true)]
    [InlineData(null, false, false, true)]
    [InlineData(false, null, false, false)]
    [InlineData(false, true, true, false)]
    public async Task When_Create_Given_TwoChildBusAndOneHasAutoStartConsumersAsOff_Then_OnlyChildBusIsStarted(bool? rootBusEnabled, bool? child1Enabled, bool child1ShouldStart, bool child2ShouldStart)
    {
        // arrange
        BusBuilder = MessageBusBuilder
            .Create()
            .WithServiceProvider(_serviceProviderMock.Object)
            .WithProviderHybrid();

        Mock<MessageBusBase> childBusMock1 = null;
        Mock<MessageBusBase> childBusMock2 = null;

        if (rootBusEnabled != null)
        {
            BusBuilder.AutoStartConsumersEnabled(rootBusEnabled.Value);
        }

        BusBuilder.AddChildBus("child1", mbb =>
        {

            if (child1Enabled != null)
            {
                mbb.AutoStartConsumersEnabled(child1Enabled.Value);
            }
            mbb.WithProvider((s) =>
            {
                var childBusSettings = new MessageBusSettings(s)
                {
                    Name = "child1"
                };
                childBusMock1 = new Mock<MessageBusBase>(childBusSettings) { CallBase = true };
                return childBusMock1.Object;
            });
        });

        BusBuilder.AddChildBus("child2", mbb =>
        {
            mbb.WithProvider((s) =>
            {
                var childBusSettings = new MessageBusSettings(s)
                {
                    Name = "child2"
                };
                childBusMock2 = new Mock<MessageBusBase>(childBusSettings) { CallBase = true };
                return childBusMock2.Object;
            });
        });


        // act
        var bus = CreateMessageBus<IMasterMessageBus>();

        await Task.Delay(TimeSpan.FromSeconds(2));

        // assert
        childBusMock1.Should().NotBeNull();
        childBusMock2.Should().NotBeNull();

        childBusMock1.Verify(x => x.AutoStart(It.IsAny<CancellationToken>()), Times.Once);
        childBusMock2.Verify(x => x.AutoStart(It.IsAny<CancellationToken>()), Times.Once);
        childBusMock1.Verify(x => x.OnStart(), child1ShouldStart ? Times.Once : Times.Never);
        childBusMock2.Verify(x => x.OnStart(), child2ShouldStart ? Times.Once : Times.Never);
    }

    [Fact]
    public async Task When_Create_Then_BusLifecycleCreatedIsSentToRegisteredInterceptors()
    {
        // arrange
        var busLifecycleInterceptorMock = new Mock<IMessageBusLifecycleInterceptor>();

        _serviceProviderMock
            .Setup(x => x.GetService(typeof(IEnumerable<IMessageBusLifecycleInterceptor>)))
            .Returns(new IMessageBusLifecycleInterceptor[] { busLifecycleInterceptorMock.Object });

        // act
        BusBuilder.Build();

        // assert
        await busLifecycleInterceptorMock
            .VerifyWithRetry(
                // give some time for the fire & forget task to complete
                TimeSpan.FromSeconds(2),
                x => x.OnBusLifecycle(MessageBusLifecycleEventType.Created, It.IsAny<IMessageBus>()),
                Times.Once());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task When_Produce_Given_LongRunningCreateInterceptor_Then_ProduceWaitsUntilInterceptorFinishes(bool isPublish)
    {
        // arrange
        var longRunnitIntitTask = Task.Delay(4000);

        var busLifecycleInterceptorMock = new Mock<IMessageBusLifecycleInterceptor>();
        busLifecycleInterceptorMock
            .Setup(x => x.OnBusLifecycle(MessageBusLifecycleEventType.Created, It.IsAny<IMessageBus>()))
            .Returns(() => longRunnitIntitTask);

        _serviceProviderMock
            .Setup(x => x.GetService(typeof(IEnumerable<IMessageBusLifecycleInterceptor>)))
            .Returns(new IMessageBusLifecycleInterceptor[] { busLifecycleInterceptorMock.Object });

        // setup responses for requests
        Bus.OnReply = (type, topic, request) =>
        {
            if (request is RequestA req)
            {
                return new ResponseA { Id = req.Id };
            }
            return null;
        };

        BusBuilder.Build();

        // act
        if (isPublish)
        {
            await Bus.ProducePublish(new RequestA());
        }
        else
        {
            await Bus.ProduceSend<ResponseA>(new RequestA());
        }

        // assert
        longRunnitIntitTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task When_NoTimeoutProvided_Then_TakesDefaultTimeoutForRequestTypeAsync()
    {
        // arrange
        var ra = new RequestA();
        var rb = new RequestB();

        // act
        var raTask = Bus.ProduceSend<ResponseA>(ra);
        var rbTask = Bus.ProduceSend<ResponseB>(rb);

        // after 10 seconds
        _timeProvider.SetUtcNow(_timeZero.AddSeconds(TimeoutFor5 + 1));
        Bus.TriggerPendingRequestCleanup();

        // assert
        await WaitForTasks(2000, raTask, rbTask);

        raTask.IsCanceled.Should().BeTrue();
        rbTask.IsCanceled.Should().BeFalse();

        // after 20 seconds
        _timeProvider.SetUtcNow(_timeZero.AddSeconds(TimeoutDefault10 + 1));
        Bus.TriggerPendingRequestCleanup();

        await WaitForTasks(2000, rbTask);

        // assert
        rbTask.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task When_ResponseArrives_Then_ResolvesPendingRequestAsync()
    {
        // arrange
        var r = new RequestA();

        Bus.OnReply = (type, topic, request) =>
        {
            if (topic == "a-requests")
            {
                var req = (RequestA)request;
                return new ResponseA { Id = req.Id };
            }
            return null;
        };

        // act
        var rTask = Bus.ProduceSend<ResponseA>(r);

        // Wait for the task to complete (response should arrive quickly in this test)
        var completedTask = await Task.WhenAny(rTask, Task.Delay(2000));

        // assert
        completedTask.Should().Be(rTask, "The request task should complete before timeout");
        rTask.IsCompleted.Should().BeTrue("Response should be completed");

        var response = await rTask;
        response.Should().NotBeNull("Response should not be null");
        response.Id.Should().Be(r.Id, "Response ID should match request ID");

        // Now trigger cleanup - the response should already be processed
        Bus.TriggerPendingRequestCleanup();

        Bus.PendingRequestsCount.Should().Be(0, "There should be no pending requests after response is received");
    }

    [Fact]
    public async Task When_ResponseArrivesTooLate_Then_ExpiresPendingRequestAsync()
    {
        // arrange
        var r1 = new RequestA();
        var r2 = new RequestA();
        var r3 = new RequestA();

        Bus.OnReply = (type, topic, request) =>
        {
            if (topic == "a-requests")
            {
                var req = (RequestA)request;
                // resolve only r1 request
                if (req.Id == r1.Id)
                {
                    return new ResponseA { Id = req.Id };
                }
            }
            return null;
        };

        // act
        var r1Task = Bus.ProduceSend<ResponseA>(r1);
        var r2Task = Bus.ProduceSend<ResponseA>(r2, timeout: TimeSpan.FromSeconds(1));
        var r3Task = Bus.ProduceSend<ResponseA>(r3);

        // 2 seconds later
        _timeProvider.SetUtcNow(_timeZero.AddSeconds(2));
        Bus.TriggerPendingRequestCleanup();

        await WaitForTasks(2000, r1Task, r2Task, r3Task);

        // assert
        r1Task.IsCompleted.Should().BeTrue("Response 1 should be completed");
        r2Task.IsCanceled.Should().BeTrue("Response 2 should be canceled");
        r3Task.IsCompleted.Should().BeFalse("Response 3 should still be pending");
        Bus.PendingRequestsCount.Should().Be(1, "There should be only 1 pending request");
    }

    [Fact]
    public async Task When_CancellationTokenCancelled_Then_CancelsPendingRequest()
    {
        // arrange
        var r1 = new RequestA();
        var r2 = new RequestA();

        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        cts2.Cancel();
        var r1Task = Bus.ProduceSend<ResponseA>(r1, cancellationToken: cts1.Token);
        var r2Task = Bus.ProduceSend<ResponseA>(r2, cancellationToken: cts2.Token);

        // act
        Bus.TriggerPendingRequestCleanup();
        await Task.WhenAny(r1Task, r2Task);

        // assert
        r1Task.IsCompleted.Should().BeFalse("Request 1 is still pending");

        r2Task.IsCanceled.Should().BeTrue("Request 2 was canceled");
        r2Task.IsFaulted.Should().BeFalse("Request 2 was canceled");
    }

    private static async Task WaitForTasks(int millis, params Task[] tasks)
    {
        try
        {
            await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(millis));
        }
        catch (Exception)
        {
            // swallow
        }
    }

    [Fact]
    public async Task When_Produce_DerivedMessage_Given_OnlyBaseMessageConfigured_Then_BaseMessageProducerConfigUsed()
    {
        // arrange
        var someMessageTopic = "some-messages";

        BusBuilder
            .Produce<SomeMessage>(x => x.DefaultTopic(someMessageTopic));

        var m = new SomeDerivedMessage();

        // act
        await Bus.ProducePublish(m);

        // assert
        _producedMessages.Count.Should().Be(1);
        // The messageType should be the ProducerSettings.MessageType (SomeMessage), not the actual message type (SomeDerivedMessage)
        _producedMessages.Should().ContainSingle(x => x.MessageType == typeof(SomeMessage) && ReferenceEquals(x.Message, m) && x.Path == someMessageTopic);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task When_Publish_DerivedMessage_Given_DeriveMessageConfigured_Then_DerivedMessageProducerConfigUsed(int caseId)
    {
        // arrange
        var someMessageTopic = "some-messages";
        var someMessageDerived2Topic = "some-messages-2";

        BusBuilder
            .Produce<SomeMessage>(x => x.DefaultTopic(someMessageTopic))
            .Produce<SomeDerived2Message>(x => x.DefaultTopic(someMessageDerived2Topic));

        var m = new SomeDerived2Message();

        // act
        if (caseId == 1)
        {
            // act
            await Bus.ProducePublish(m);

        }

        if (caseId == 2)
        {
            // act
            await Bus.ProducePublish(m);
        }

        if (caseId == 3)
        {
            // act
            await Bus.ProducePublish(m);
        }

        // assert
        _producedMessages.Count.Should().Be(1);
        _producedMessages.Should().ContainSingle(x => x.MessageType == typeof(SomeDerived2Message) && ReferenceEquals(x.Message, m) && x.Path == someMessageDerived2Topic);
    }

    [Fact]
    public void When_GetProducerSettings_Given_MessageWasDeclared_Then_GetProducerSettingsShouldNotReturnNull()
    {
        // arrange
        var someMessageTopic = "some-messages";

        BusBuilder
            .Produce<SomeMessage>(x => x.DefaultTopic(someMessageTopic))
            .Produce<SomeRequest>(x => x.DefaultTopic(someMessageTopic));

        // act
        var producerSettings1 = Bus.Public_GetProducerSettings(typeof(SomeMessage));
        var producerSettings2 = Bus.Public_GetProducerSettings(typeof(SomeRequest));

        // assert
        producerSettings1.Should().NotBeNull();
        producerSettings2.Should().NotBeNull();
    }

    [Fact]
    public void When_GetProducerSettings_Given_RequestMessageHandled_Then_GetProducerSettingsShouldReturnNull()
    {
        // arrange
        var someMessageTopic = "some-messages";

        BusBuilder
            .Handle<SomeRequest, SomeResponse>(x => x.Topic(someMessageTopic).WithHandler<SomeRequestMessageHandler>());

        // act
        var producerSettings = Bus.Public_GetProducerSettings(typeof(SomeResponse));

        // assert
        producerSettings.Should().NotBeNull();
    }

    [Fact]
    public void When_Produce_Message_Given_MessageNotDeclared_Then_GetProducerSettingsShouldThrowException()
    {
        // arrange
        var someMessageTopic = "some-messages";

        BusBuilder
            .Produce<SomeMessage>(x => x.DefaultTopic(someMessageTopic));

        // act
        Action action = () => Bus.Public_GetProducerSettings(typeof(SomeRequest));

        // assert
        action.Should().Throw<ProducerMessageBusException>();
    }

    [Fact]
    public async Task When_Publish_Given_Disposed_Then_ThrowsException()
    {
        // arrange
        Bus.Dispose();

        // act
        Func<Task> act = async () => await Bus.ProducePublish(new SomeMessage());
        Func<Task> actWithTopic = async () => await Bus.ProducePublish(new SomeMessage(), "some-topic");

        // assert
        await act.Should().ThrowAsync<MessageBusException>();
        await actWithTopic.Should().ThrowAsync<MessageBusException>();
    }

    [Fact]
    public async Task When_Send_Given_Disposed_Then_ThrowsException()
    {
        // arrange
        Bus.Dispose();

        // act
        Func<Task> act = async () => await Bus.ProduceSend<SomeResponse>(new SomeRequest());
        Func<Task> actWithTopic = async () => await Bus.ProduceSend<SomeResponse>(new SomeRequest(), "some-topic");

        // assert
        await act.Should().ThrowAsync<MessageBusException>();
        await actWithTopic.Should().ThrowAsync<MessageBusException>();
    }

    [Theory]
    [InlineData(1, null, null)]   // no interceptors
    [InlineData(1, true, true)]   // both interceptors call next()
    [InlineData(1, true, null)]   // producer interceptor calls next(), other does not exist
    [InlineData(1, null, true)]   // publish interceptor calls next(), other does not exist
    [InlineData(0, false, false)] // none of the interceptors calls next()
    [InlineData(0, false, null)]  // producer interceptor does not call next()
    [InlineData(0, null, false)]  // publish interceptor does not call next()
    public async Task When_Publish_Given_InterceptorsInDI_Then_InterceptorInfluenceIfTheMessageIsDelivered(
        int producedMessages, bool? producerInterceptorCallsNext, bool? publishInterceptorCallsNext)
    {
        // arrange
        var topic = "some-messages";

        BusBuilder
            .Produce<SomeMessage>(x => x.DefaultTopic(topic));

        var m = new SomeDerivedMessage();

        var producerInterceptorMock = new Mock<IProducerInterceptor<SomeDerivedMessage>>();
        producerInterceptorMock.Setup(x => x.OnHandle(m, It.IsAny<Func<Task<object>>>(), It.IsAny<IProducerContext>()))
            .Returns((SomeDerivedMessage m, Func<Task<object>> next, IProducerContext context)
                => producerInterceptorCallsNext == true ? next() : Task.FromResult<object>(null));

        var publishInterceptorMock = new Mock<IPublishInterceptor<SomeDerivedMessage>>();
        publishInterceptorMock.Setup(x => x.OnHandle(m, It.IsAny<Func<Task>>(), It.IsAny<IProducerContext>()))
            .Returns((SomeDerivedMessage m, Func<Task> next, IProducerContext context)
                => publishInterceptorCallsNext == true ? next() : Task.CompletedTask);

        if (producerInterceptorCallsNext != null)
        {
            _serviceProviderMock
                .Setup(x => x.GetService(typeof(IEnumerable<IProducerInterceptor<SomeDerivedMessage>>)))
                .Returns(new[] { producerInterceptorMock.Object });
        }

        if (publishInterceptorCallsNext != null)
        {
            _serviceProviderMock
                .Setup(x => x.GetService(typeof(IEnumerable<IPublishInterceptor<SomeDerivedMessage>>)))
                .Returns(new[] { publishInterceptorMock.Object });
        }

        // act
        await Bus.ProducePublish(m);

        // assert

        // message delivered
        _producedMessages.Count.Should().Be(producedMessages);

        _serviceProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IProducerInterceptor<SomeDerivedMessage>>)), Times.Once);
        _serviceProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IProducerInterceptor<SomeMessage>>)), Times.Never);
        _serviceProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IPublishInterceptor<SomeDerivedMessage>>)), Times.Once);
        _serviceProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IPublishInterceptor<SomeMessage>>)), Times.Never);

        VerifyCommonServiceProvider();

        _serviceProviderMock.VerifyNoOtherCalls();

        if (producerInterceptorCallsNext != null)
        {
            producerInterceptorMock.Verify(x => x.OnHandle(m, It.IsAny<Func<Task<object>>>(), It.IsAny<IProducerContext>()), Times.Once);
        }
        producerInterceptorMock.VerifyNoOtherCalls();

        if (publishInterceptorCallsNext != null)
        {
            // ProducePublish interceptor is called after Producer interceptor, if producer does not call next() the publish interceptor does not get a chance to fire
            if (producerInterceptorCallsNext == null || producerInterceptorCallsNext == true)
            {
                publishInterceptorMock.Verify(x => x.OnHandle(m, It.IsAny<Func<Task>>(), It.IsAny<IProducerContext>()), Times.Once);
            }
        }
        publishInterceptorMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_Publish_DerivedMessage_Given_DerivedConfigured_Then_SerializerReceivesDerivedMessageType()
    {
        // arrange
        var someMessageTopic = "some-messages";
        var someDerived2MessageTopic = "some-derived2-messages";

        var serializerMock = new Mock<IMessageSerializer>();
        serializerMock
            .Setup(x => x.Serialize(It.IsAny<Type>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<object>(), It.IsAny<object>()))
            .Returns([1, 2, 3]);

        var serializerProviderMock = new Mock<IMessageSerializerProvider>();
        serializerProviderMock
            .Setup(x => x.GetSerializer(It.IsAny<string>()))
            .Returns(serializerMock.Object);

        // Create a fresh service provider mock for this test
        var testServiceProviderMock = new Mock<IServiceProvider>();
        testServiceProviderMock.Setup(x => x.GetService(typeof(IMessageSerializerProvider))).Returns(serializerProviderMock.Object);
        testServiceProviderMock.Setup(x => x.GetService(typeof(IMessageTypeResolver))).Returns(new AssemblyQualifiedNameMessageTypeResolver([]));
        testServiceProviderMock.Setup(x => x.GetService(typeof(TimeProvider))).Returns(() => _timeProvider);
        testServiceProviderMock.Setup(x => x.GetService(It.Is<Type>(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)))).Returns((Type t) => Array.CreateInstance(t.GetGenericArguments()[0], 0));
        testServiceProviderMock.Setup(x => x.GetService(typeof(RuntimeTypeCache))).Returns(new RuntimeTypeCache());
        testServiceProviderMock.Setup(x => x.GetService(typeof(IPendingRequestManager))).Returns(() => new PendingRequestManager(new InMemoryPendingRequestStore(), _timeProvider, NullLoggerFactory.Instance));

        var testBusBuilder = MessageBusBuilder.Create()
            .Produce<SomeMessage>(x => x.DefaultTopic(someMessageTopic))
            .Produce<SomeDerived2Message>(x => x.DefaultTopic(someDerived2MessageTopic))
            .WithServiceProvider(testServiceProviderMock.Object)
            .WithProvider(s => new MessageBusTested(s, _timeProvider));

        var testBus = testBusBuilder.Build() as MessageBusTested;

        testBus.SerializeAllMessages = true; // Enable serialization for all messages
        testBus.OnProduced = (messageType, path, message) => { }; // Initialize callback to avoid null reference

        var derived2Message = new SomeDerived2Message();

        // act
        await testBus.ProducePublish(derived2Message);

        // assert
        // The serializer should be called with the ProducerSettings.MessageType (SomeDerived2Message) when exact type is configured
        serializerMock.Verify(
            x => x.Serialize(
                typeof(SomeDerived2Message), // ProducerSettings.MessageType for the derived type
                It.IsAny<IDictionary<string, object>>(),
                derived2Message, // actual message instance
                It.IsAny<object>()),
            Times.Once);

        testBus.Dispose();
    }

    [Theory]
    [InlineData(false, false, 1)] // Single message, no interceptor, base type configured
    [InlineData(false, true, 1)]  // Single message, with interceptor, base type configured
    [InlineData(true, false, 3)]  // Bulk publish, no interceptor, base type configured
    [InlineData(true, true, 3)]   // Bulk publish, with interceptor, base type configured
    public async Task When_PublishBulk_DerivedMessages_Given_BaseConfigured_Then_SerializerReceivesProducerSettingsMessageType(bool isBulk, bool withInterceptor, int messageCount)
    {
        // arrange
        var someMessageTopic = "some-messages";

        var serializerMock = new Mock<IMessageSerializer>();
        serializerMock
            .Setup(x => x.Serialize(It.IsAny<Type>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<object>(), It.IsAny<object>()))
            .Returns([1, 2, 3]);

        var serializerProviderMock = new Mock<IMessageSerializerProvider>();
        serializerProviderMock
            .Setup(x => x.GetSerializer(It.IsAny<string>()))
            .Returns(serializerMock.Object);

        // Create a fresh service provider mock for this test
        var testServiceProviderMock = new Mock<IServiceProvider>();
        testServiceProviderMock.Setup(x => x.GetService(typeof(IMessageSerializerProvider))).Returns(serializerProviderMock.Object);
        testServiceProviderMock.Setup(x => x.GetService(typeof(IMessageTypeResolver))).Returns(new AssemblyQualifiedNameMessageTypeResolver([]));
        testServiceProviderMock.Setup(x => x.GetService(typeof(TimeProvider))).Returns(() => _timeProvider);
        testServiceProviderMock.Setup(x => x.GetService(It.Is<Type>(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)))).Returns((Type t) => Array.CreateInstance(t.GetGenericArguments()[0], 0));
        testServiceProviderMock.Setup(x => x.GetService(typeof(RuntimeTypeCache))).Returns(new RuntimeTypeCache());
        testServiceProviderMock.Setup(x => x.GetService(typeof(IPendingRequestManager))).Returns(() => new PendingRequestManager(new InMemoryPendingRequestStore(), _timeProvider, NullLoggerFactory.Instance));

        if (withInterceptor)
        {
            // Setup producer interceptor
            var producerInterceptorMock = new Mock<IProducerInterceptor<SomeDerivedMessage>>();
            producerInterceptorMock
                .Setup(x => x.OnHandle(It.IsAny<SomeDerivedMessage>(), It.IsAny<Func<Task<object>>>(), It.IsAny<IProducerContext>()))
                .Returns((SomeDerivedMessage m, Func<Task<object>> next, IProducerContext context) => next());

            testServiceProviderMock
                .Setup(x => x.GetService(typeof(IEnumerable<IProducerInterceptor<SomeDerivedMessage>>)))
                .Returns(new[] { producerInterceptorMock.Object });

            // Setup publish interceptor  
            var publishInterceptorMock = new Mock<IPublishInterceptor<SomeDerivedMessage>>();
            publishInterceptorMock
                .Setup(x => x.OnHandle(It.IsAny<SomeDerivedMessage>(), It.IsAny<Func<Task>>(), It.IsAny<IProducerContext>()))
                .Returns((SomeDerivedMessage m, Func<Task> next, IProducerContext context) => next());

            testServiceProviderMock
                .Setup(x => x.GetService(typeof(IEnumerable<IPublishInterceptor<SomeDerivedMessage>>)))
                .Returns(new[] { publishInterceptorMock.Object });
        }

        var testBusBuilder = MessageBusBuilder.Create()
            .Produce<SomeMessage>(x => x.DefaultTopic(someMessageTopic))
            .WithServiceProvider(testServiceProviderMock.Object)
            .WithProvider(s => new MessageBusTested(s, _timeProvider));

        var testBus = testBusBuilder.Build() as MessageBusTested;

        testBus.SerializeAllMessages = true; // Enable serialization for all messages
        testBus.OnProduced = (messageType, path, message) => { }; // Initialize callback to avoid null reference

        // act
        if (isBulk)
        {
            var derivedMessages = new[] { new SomeDerivedMessage(), new SomeDerivedMessage(), new SomeDerivedMessage() };
            await testBus.ProducePublish(derivedMessages);
        }
        else
        {
            var derivedMessage = new SomeDerivedMessage();
            await testBus.ProducePublish(derivedMessage);
        }

        // assert
        // The serializer should be called with the ProducerSettings.MessageType (SomeMessage), not the actual message type (SomeDerivedMessage)
        // This should work the same regardless of whether interceptors are present or if it's bulk/single
        serializerMock.Verify(
            x => x.Serialize(
                typeof(SomeMessage), // ProducerSettings.MessageType
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<SomeDerivedMessage>(), // actual message instance
                It.IsAny<object>()),
            Times.Exactly(messageCount));

        testBus.Dispose();
    }

    private void VerifyCommonServiceProvider()
    {
        _serviceProviderMock.Verify(x => x.GetService(typeof(IEnumerable<IMessageBusLifecycleInterceptor>)), Times.Between(0, 2, Moq.Range.Inclusive));
        _serviceProviderMock.Verify(x => x.GetService(typeof(ILoggerFactory)), Times.Once);
        _serviceProviderMock.Verify(x => x.GetService(typeof(IMessageSerializerProvider)), Times.Between(0, 1, Moq.Range.Inclusive));
        _serviceProviderMock.Verify(x => x.GetService(typeof(IMessageTypeResolver)), Times.Once);
        _serviceProviderMock.Verify(x => x.GetService(typeof(TimeProvider)), Times.Once);
        _serviceProviderMock.Verify(x => x.GetService(typeof(RuntimeTypeCache)), Times.Once);
        _serviceProviderMock.Verify(x => x.GetService(typeof(IPendingRequestManager)), Times.Once);
    }

    [Fact]
    public async Task When_Start_Given_ConcurrentCalls_Then_ItOnlyStartsConsumersOnce()
    {
        ThreadPool.SetMinThreads(100, 100);

        // arrange
        BusBuilder
            .Consume<SomeMessage>(x => x.Topic("topic"));

        // trigger lazy bus creation here ahead of the Tasks
        var bus = Bus;

        await bus.Start();

        // act
        for (var i = 0; i < 10; i++)
        {
            await Task.WhenAll(Enumerable.Range(0, 10000).Select(x => bus.Start()).AsParallel());
        }

        // assert
        bus._stoppedCount.Should().Be(0);
        bus._startedCount.Should().Be(1);
    }

    [Fact]
    public async Task When_Stop_Given_ConcurrentCalls_Then_ItOnlyStopsConsumersOnce()
    {
        // arrange
        BusBuilder
            .Consume<SomeMessage>(x => x.Topic("topic"));

        // trigger lazy bus creation here ahead of the Tasks
        var bus = Bus;

        await bus.Start();

        // act
        for (var i = 0; i < 10; i++)
        {
            await Task.WhenAll(Enumerable.Range(0, 10000).Select(x => bus.Stop()).AsParallel());
        }

        // assert
        bus._stoppedCount.Should().Be(1);
        bus._startedCount.Should().Be(1);
    }

    public class ProduceResponseTests
    {
        [Fact]
        public async Task When_Given_NoReplyToHeader_DoNothing()
        {
            // arrange
            var requestId = "req-123";
            var request = new object();
            var response = new object();

            object value;
            var mockRequestHeaders = new Mock<IReadOnlyDictionary<string, object>>();
            mockRequestHeaders
                .Setup(x => x.TryGetValue(ReqRespMessageHeaders.ReplyTo, out value))
                .Returns(false).Verifiable(Times.Once);

            var mockMessageTypeResolver = new Mock<IMessageTypeResolver>();

            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(x => x.GetService(typeof(IMessageTypeResolver))).Returns(mockMessageTypeResolver.Object);
            mockServiceProvider.Setup(x => x.GetService(typeof(TimeProvider))).Returns(TimeProvider.System);
            mockServiceProvider.Setup(x => x.GetService(typeof(RuntimeTypeCache))).Returns(new RuntimeTypeCache());
            mockServiceProvider.Setup(x => x.GetService(typeof(IPendingRequestManager))).Returns(new PendingRequestManager(new InMemoryPendingRequestStore(), TimeProvider.System, NullLoggerFactory.Instance));

            var mockMessageTypeConsumerInvokerSettings = new Mock<IMessageTypeConsumerInvokerSettings>();
            mockMessageTypeConsumerInvokerSettings.SetupGet(x => x.ParentSettings).Returns(() => new ConsumerSettings() { ResponseType = response.GetType() });

            var settings = new MessageBusSettings { ServiceProvider = mockServiceProvider.Object };

            var mockMessageBus = new Mock<MessageBusBase>(settings) { CallBase = true };

            var target = mockMessageBus.Object;

            // act
            await target.ProduceResponse(requestId, request, mockRequestHeaders.Object, response, null, mockMessageTypeConsumerInvokerSettings.Object, It.IsAny<CancellationToken>());

            // assert
            mockRequestHeaders.VerifyAll();
            mockMessageBus.VerifyAll();
        }
    }
}
