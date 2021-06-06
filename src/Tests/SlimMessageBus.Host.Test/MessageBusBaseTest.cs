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
    using SlimMessageBus.Host.Serialization.Json;
    using Xunit;

    public class RequestA : IRequestMessage<ResponseA>
    {
        public string Id { get; set; }

        public RequestA()
        {
            Id = Guid.NewGuid().ToString();
        }
    }

    public class ResponseA
    {
        public string Id { get; set; }
    }

    public class RequestB : IRequestMessage<ResponseB>
    {

    }

    public class ResponseB
    {
    }

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
        public void WhenCreateGivenConfigurationThatDeclaresSameMessageTypeMoreThanOnceThenExceptionIsThrown()
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
        public void WhenNoTimeoutProvidedThenTakesDefaultTimeoutForRequestType()
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
        public void WhenResponseArrivesThenResolvesPendingRequest()
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
        public void WhenResponseArrivesTooLateThenExpiresPendingRequest()
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
        public async Task WhenCancellationTokenCancelledThenCancellsPendingRequest()
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
        public void WhenRequestMessageSerializedThenDeserializeGivesSameObject()
        {
            // arrange
            var r = new RequestA();
            var rid = "1";
            var replyTo = "some_topic";
            var expires = DateTimeOffset.UtcNow.AddMinutes(2);
            var reqMessage = new MessageWithHeaders();
            reqMessage.SetHeader(ReqRespMessageHeaders.ReplyTo, replyTo);
            reqMessage.SetHeader(ReqRespMessageHeaders.RequestId, rid);
            reqMessage.SetHeader(ReqRespMessageHeaders.Expires, expires);

            // act
            var payload = Bus.SerializeRequest(typeof(RequestA), r, reqMessage, new Mock<ProducerSettings>().Object);
            Bus.DeserializeRequest(typeof(RequestA), payload, out var resMessage);

            // assert
            resMessage.Headers[ReqRespMessageHeaders.RequestId].Should().Be(rid);
            resMessage.Headers[ReqRespMessageHeaders.ReplyTo].Should().Be(replyTo);
            resMessage.TryGetHeader(ReqRespMessageHeaders.Expires, out DateTimeOffset? resExpires);

            resExpires.HasValue.Should().BeTrue();
            resExpires.Value.ToFileTime().Should().Be(expires.ToFileTime());
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
        public void GivenDisposedWhenPublishThenThrowsException()
        {
            // arrange
            Bus.Dispose();

            // act
            Func<Task> act = async () => await Bus.Publish(new SomeMessage()).ConfigureAwait(false);
            Func<Task> actWithTopic = async () => await Bus.Publish(new SomeMessage(), "some-topic").ConfigureAwait(false);

            // assert
            act.Should().Throw<MessageBusException>();
            actWithTopic.Should().Throw<MessageBusException>();
        }

        [Fact]
        public void GivenDisposedWhenSendThenThrowsException()
        {
            // arrange
            Bus.Dispose();

            // act
            Func<Task> act = async () => await Bus.Send(new SomeRequest()).ConfigureAwait(false);
            Func<Task> actWithTopic = async () => await Bus.Send(new SomeRequest(), "some-topic").ConfigureAwait(false);

            // assert
            act.Should().Throw<MessageBusException>();
            actWithTopic.Should().Throw<MessageBusException>();
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

        public int PendingRequestsCount => PendingRequestStore.GetCount();

        public Func<Type, string, object, object> OnReply { get; set; }
        public Action<Type, string, object> OnProduced { get; set; }

        #region Overrides of BaseMessageBus

        public override Task ProduceToTransport(Type messageType, object message, string name, byte[] payload, MessageWithHeaders messageWithHeaders = null)
        {
            OnProduced(messageType, name, message);

            if (messageType.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IRequestMessage<>)))
            {
                var req = DeserializeRequest(messageType, payload, out var requestMessage);

                var resp = OnReply(messageType, name, req);
                if (resp == null)
                {
                    return Task.CompletedTask;
                }

                var respMessage = new MessageWithHeaders();
                respMessage.SetHeader(ReqRespMessageHeaders.RequestId, requestMessage.Headers[ReqRespMessageHeaders.RequestId]);
                var replyTo = requestMessage.Headers[ReqRespMessageHeaders.ReplyTo];

                var respPayload = SerializeResponse(resp.GetType(), resp, respMessage);
                return OnResponseArrived(respPayload, replyTo);
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
