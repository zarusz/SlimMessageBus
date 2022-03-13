namespace SlimMessageBus.Host.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Moq;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.DependencyResolver;
    using SlimMessageBus.Host.Serialization;
    using SlimMessageBus.Host.Serialization.Json;
    using Xunit;

    public class RequestA : IRequestMessage<ResponseA>
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
    }

    public class ResponseA
    {
        public string Id { get; set; }
    }

    public class RequestB : IRequestMessage<ResponseB> { }

    public class ResponseB { }

    public class MessageBusBaseTest : IDisposable
    {
        private MessageBusBuilder BusBuilder { get; }
        private readonly Lazy<MessageBusTested> _busLazy;
        private MessageBusTested Bus => _busLazy.Value;
        private readonly DateTimeOffset _timeZero;
        private DateTimeOffset _timeNow;

        private const int TimeoutForA10 = 10;
        private const int TimeoutDefault20 = 20;

        public IList<(Type messageType, string name, object message)> _producedMessages;

        public MessageBusBaseTest()
        {
            _timeZero = DateTimeOffset.Now;
            _timeNow = _timeZero;

            _producedMessages = new List<(Type messageType, string name, object message)>();

            BusBuilder = MessageBusBuilder.Create()
                .Produce<RequestA>(x =>
                {
                    x.DefaultTopic("a-requests");
                    x.DefaultTimeout(TimeSpan.FromSeconds(TimeoutForA10));
                })
                .Produce<RequestB>(x =>
                {
                    x.DefaultTopic("b-requests");
                })
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic("app01-responses");
                    x.DefaultTimeout(TimeSpan.FromSeconds(TimeoutDefault20));
                })
                .WithDependencyResolver(new LookupDependencyResolver(t => null))
                .WithSerializer(new JsonMessageSerializer())
                .WithProvider(s =>
                {
                    var bus = new MessageBusTested(s)
                    {
                        // provide current time
                        CurrentTimeProvider = () => _timeNow,
                        OnProduced = (mt, n, m) => _producedMessages.Add((mt, n, m))
                    };
                    return bus;
                });

            _busLazy = new Lazy<MessageBusTested>(() => (MessageBusTested)BusBuilder.Build());
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
            BusBuilder.Produce<RequestA>(x =>
            {
                x.DefaultTopic("default-topic");
            });
            BusBuilder.Produce<RequestA>(x =>
            {
                x.DefaultTopic("default-topic-2");
            });

            // act

            Action busCreation = () => BusBuilder.Build();

            // assert
            busCreation.Should().Throw<ConfigurationMessageBusException>()
                .WithMessage("*was declared more than once*");
        }

        [Fact]
        public void When_NoTimeoutProvided_Then_TakesDefaultTimeoutForRequestType()
        {
            // arrange
            var ra = new RequestA();
            var rb = new RequestB();

            // act
            var raTask = Bus.Send(ra);
            var rbTask = Bus.Send(rb);

            WaitForTasks(2000, raTask, rbTask);

            // after 10 seconds
            _timeNow = _timeZero.AddSeconds(TimeoutForA10 + 1);
            Bus.TriggerPendingRequestCleanup();

            // assert
            raTask.IsCanceled.Should().BeTrue();
            rbTask.IsCanceled.Should().BeFalse();

            // adter 20 seconds
            _timeNow = _timeZero.AddSeconds(TimeoutDefault20 + 1);
            Bus.TriggerPendingRequestCleanup();

            // assert
            rbTask.IsCanceled.Should().BeTrue();
        }

        [Fact]
        public void When_ResponseArrives_Then_ResolvesPendingRequest()
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
            var rTask = Bus.Send(r);
            WaitForTasks(2000, rTask);
            Bus.TriggerPendingRequestCleanup();

            // assert
            rTask.IsCompleted.Should().BeTrue("Response should be completed");
            r.Id.Should().Be(rTask.Result.Id);

            Bus.PendingRequestsCount.Should().Be(0, "There should be no pending requests");
        }

        [Fact]
        public void When_ResponseArrivesTooLate_Then_ExpiresPendingRequest()
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
            var r1Task = Bus.Send(r1);
            var r2Task = Bus.Send(r2, TimeSpan.FromSeconds(1));
            var r3Task = Bus.Send(r3);

            // 2 seconds later
            _timeNow = _timeZero.AddSeconds(2);
            Bus.TriggerPendingRequestCleanup();

            WaitForTasks(2000, r1Task, r2Task, r3Task);

            // assert
            r1Task.IsCompleted.Should().BeTrue("Response 1 should be completed");
            r2Task.IsCanceled.Should().BeTrue("Response 2 should be canceled");
            r3Task.IsCompleted.Should().BeFalse("Response 3 should still be pending");
            Bus.PendingRequestsCount.Should().Be(1, "There should be only 1 pending request");
        }

        [Fact]
        public async Task When_CancellationTokenCancelled_Then_CancellsPendingRequest()
        {
            // arrange
            var r1 = new RequestA();
            var r2 = new RequestA();

            var cts1 = new CancellationTokenSource();
            var cts2 = new CancellationTokenSource();

            cts2.Cancel();
            var r1Task = Bus.Send(r1, cts1.Token);
            var r2Task = Bus.Send(r2, cts2.Token);

            // act
            Bus.TriggerPendingRequestCleanup();
            await Task.WhenAny(r1Task, r2Task);

            // assert
            r1Task.IsCompleted.Should().BeFalse("Request 1 is still pending");

            r2Task.IsCanceled.Should().BeTrue("Request 2 was canceled");
            r2Task.IsFaulted.Should().BeFalse("Request 2 was canceled");

            cts1.Dispose();
            cts2.Dispose();
        }

        private static void WaitForTasks(int millis, params Task[] tasks)
        {
            try
            {
                Task.WaitAll(tasks, millis);
            }
            catch (AggregateException)
            {
                // swallow
            }
        }

        [Fact]
        public async Task When_Produce_DerivedMessage_Given_OnlyBaseMessageConfigured_Then_BaseMessageTypeIsSentToSerializer()
        {
            // arrange
            var messageSerializerMock = new Mock<IMessageSerializer>();
            messageSerializerMock.Setup(x => x.Serialize(It.IsAny<Type>(), It.IsAny<object>())).Returns(new byte[0]);

            var someMessageTopic = "some-messages";

            BusBuilder
                .Produce<SomeMessage>(x =>
                {
                    x.DefaultTopic(someMessageTopic);
                })
                .WithSerializer(messageSerializerMock.Object);

            var m1 = new SomeMessage();
            var m2 = new SomeDerivedMessage();
            var m3 = new SomeDerived2Message();

            // act
            await Bus.Publish(m1);
            await Bus.Publish(m2);
            await Bus.Publish(m3);

            // assert
            _producedMessages.Count.Should().Be(3);

            var producedMessage1 = _producedMessages.FirstOrDefault(x => object.ReferenceEquals(x.message, m1));
            producedMessage1.Should().NotBeNull();
            producedMessage1.messageType.Should().Be(typeof(SomeMessage));
            producedMessage1.name.Should().Be(someMessageTopic);

            var producedMessage2 = _producedMessages.FirstOrDefault(x => object.ReferenceEquals(x.message, m2));
            producedMessage2.Should().NotBeNull();
            producedMessage2.messageType.Should().Be(typeof(SomeMessage));
            producedMessage2.name.Should().Be(someMessageTopic);

            var producedMessage3 = _producedMessages.FirstOrDefault(x => object.ReferenceEquals(x.message, m3));
            producedMessage3.Should().NotBeNull();
            producedMessage3.messageType.Should().Be(typeof(SomeMessage));
            producedMessage3.name.Should().Be(someMessageTopic);

            messageSerializerMock.Verify(x => x.Serialize(typeof(SomeMessage), m1), Times.Once);
            messageSerializerMock.Verify(x => x.Serialize(typeof(SomeMessage), m2), Times.Once);
            messageSerializerMock.Verify(x => x.Serialize(typeof(SomeMessage), m3), Times.Once);
            messageSerializerMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task When_Produce_DerivedMessage_Given_OnlyBaseMessageConfigured_Then_BaseMessageProducerConfigUsed()
        {
            // arrange
            var someMessageTopic = "some-messages";

            BusBuilder
                .Produce<SomeMessage>(x =>
                {
                    x.DefaultTopic(someMessageTopic);
                });

            var m = new SomeDerivedMessage();

            // act
            await Bus.Publish(m);

            // assert
            _producedMessages.Count.Should().Be(1);
            _producedMessages[0].messageType.Should().Be(typeof(SomeMessage));
            _producedMessages[0].message.Should().Be(m);
            _producedMessages[0].name.Should().Be(someMessageTopic);
        }

        [Fact]
        public async Task When_Produce_DerivedMessage_Given_DeriveMessageConfigured_Then_DerivedMessageProducerConfigUsed()
        {
            // arrange
            var someMessageTopic = "some-messages";
            var someMessageDerived2Topic = "some-messages-2";

            BusBuilder
                .Produce<SomeMessage>(x =>
                {
                    x.DefaultTopic(someMessageTopic);
                })
                .Produce<SomeDerived2Message>(x =>
                {
                    x.DefaultTopic(someMessageDerived2Topic);
                });

            var m = new SomeDerived2Message();

            // act
            await Bus.Publish(m);

            // assert
            _producedMessages.Count.Should().Be(1);
            _producedMessages[0].messageType.Should().Be(typeof(SomeDerived2Message));
            _producedMessages[0].message.Should().Be(m);
            _producedMessages[0].name.Should().Be(someMessageDerived2Topic);
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
                .Produce<SomeMessage>(x =>
                {
                    x.DefaultTopic(someMessageTopic);
                });

            // act
            Action action = () => Bus.Public_GetProducerSettings(typeof(SomeRequest));

            // assert
            action.Should().Throw<PublishMessageBusException>();
        }

        [Fact]
        public async Task When_Publish_Given_Disposed_Then_ThrowsException()
        {
            // arrange
            Bus.Dispose();

            // act
            Func<Task> act = async () => await Bus.Publish(new SomeMessage()).ConfigureAwait(false);
            Func<Task> actWithTopic = async () => await Bus.Publish(new SomeMessage(), "some-topic").ConfigureAwait(false);

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
            Func<Task> act = async () => await Bus.Send(new SomeRequest()).ConfigureAwait(false);
            Func<Task> actWithTopic = async () => await Bus.Send(new SomeRequest(), "some-topic").ConfigureAwait(false);

            // assert
            await act.Should().ThrowAsync<MessageBusException>();
            await actWithTopic.Should().ThrowAsync<MessageBusException>();
        }

        [Fact]
        public async Task When_Publish_Or_Send_Then_OnMessageProducedIsCalled_AtTheProducerLevel_And_AtTheBusLevel()
        {
            // arrange
            var someMessageTopic = "some-messages";
            var someRequestTopic = "some-requests";

            var onMessageProducedMock = new Mock<Action<IMessageBus, ProducerSettings, object, string>>();

            BusBuilder
                .Produce<SomeMessage>(x =>
                {
                    x.DefaultTopic(someMessageTopic);
                    x.AttachEvents(events =>
                    {
                        events.OnMessageProduced = onMessageProducedMock.Object;
                    });
                })
                .Produce<SomeRequest>(x =>
                {
                    x.DefaultTopic(someRequestTopic);
                    x.AttachEvents(events =>
                    {
                        events.OnMessageProduced = onMessageProducedMock.Object;
                    });
                })
                .AttachEvents(events =>
                {
                    events.OnMessageProduced = onMessageProducedMock.Object;
                });

            Bus.OnReply = (type, topic, request) =>
            {
                if (topic == someRequestTopic)
                {
                    return new SomeResponse();
                }
                return null;
            };

            var m = new SomeMessage();
            var r = new SomeRequest();

            // act

            // act
            await Bus.Publish(m);
            await Bus.Send(r);

            // assert
            onMessageProducedMock.Verify(
                x => x(Bus, It.IsAny<ProducerSettings>(), m, someMessageTopic), Times.Exactly(2)); // callback twice - at the producer and bus level

            onMessageProducedMock.Verify(
                x => x(Bus, It.IsAny<ProducerSettings>(), r, someRequestTopic), Times.Exactly(2)); // callback twice - at the producer and bus level
        }
    }

    public class MessageBusTested : MessageBusBase
    {
        public MessageBusTested(MessageBusSettings settings)
            : base(settings)
        {
            // by default no responses will arrive
            OnReply = (type, payload, req) => null;

            OnBuildProvider();
        }

        public ProducerSettings Public_GetProducerSettings(Type messageType) => GetProducerSettings(messageType);

        public int PendingRequestsCount => PendingRequestStore.GetCount();

        public Func<Type, string, object, object> OnReply { get; set; }
        public Action<Type, string, object> OnProduced { get; set; }

        #region Overrides of BaseMessageBus

        public override Task ProduceToTransport(Type messageType, object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders)
        {
            OnProduced(messageType, path, message);

            if (messageType.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IRequestMessage<>)))
            {
                var req = Serializer.Deserialize(messageType, messagePayload);

                var resp = OnReply(messageType, path, req);
                if (resp == null)
                {
                    return Task.CompletedTask;
                }

                messageHeaders.TryGetHeader(ReqRespMessageHeaders.RequestId, out string replyTo);

                var resposeHeaders = CreateHeaders();
                resposeHeaders.SetHeader(ReqRespMessageHeaders.RequestId, replyTo);

                var responsePayload = Serializer.Serialize(resp.GetType(), resp);
                return OnResponseArrived(responsePayload, replyTo, resposeHeaders);
            }

            return Task.CompletedTask;
        }

        #endregion

        #region Overrides of MessageBusBase

        public override DateTimeOffset CurrentTime => CurrentTimeProvider();

        #endregion

        public Func<DateTimeOffset> CurrentTimeProvider { get; set; } = () => DateTimeOffset.UtcNow;

        public void TriggerPendingRequestCleanup()
        {
            PendingRequestManager.CleanPendingRequests();
        }
    }
}
