using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Common.Logging.Simple;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using SecretStore;
using SlimMessageBus.Host.AzureServiceBus.Config;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Serialization.Json;
using Xunit;

namespace SlimMessageBus.Host.AzureServiceBus.Test
{
    [Trait("Category", "Integration")]
    public class ServiceBusMessageBusIt : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int NumberOfMessages = 77;

        private ServiceBusMessageBusSettings Settings { get; }
        private MessageBusBuilder MessageBusBuilder { get; }
        private Lazy<ServiceBusMessageBus> MessageBus { get; }

        public ServiceBusMessageBusIt()
        {
            LogManager.Adapter = new DebugLoggerFactoryAdapter();

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            Secrets.Load(@"..\..\..\..\..\secrets.txt");

            var connectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);

            Settings = new ServiceBusMessageBusSettings(connectionString);

            MessageBusBuilder = MessageBusBuilder.Create()
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderServiceBus(Settings);

            MessageBus = new Lazy<ServiceBusMessageBus>(() => (ServiceBusMessageBus)MessageBusBuilder.Build());
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
                MessageBus.Value.Dispose();
            }
        }

        [Fact]
        public async Task BasicPubSubOnTopic()
        {
            var concurrency = 2;
            var subscribers = 2;
            var topic = "test-ping";

            MessageBusBuilder
                .Produce<PingMessage>(x => x.DefaultTopic(topic))
                .Do(builder => Enumerable.Range(0, subscribers).ToList().ForEach(i =>
                {
                    builder.SubscribeTo<PingMessage>(x => x
                        .Topic(topic)
                        .SubscriptionName($"subscriber-{i}") // ensure subscription exists on the ServiceBus topic
                        .WithSubscriber<PingConsumer>()
                        .Instances(concurrency));
                }));

            await BasicPubSub(concurrency, subscribers, subscribers).ConfigureAwait(false);
        }

        [Fact]
        public async Task BasicPubSubOnQueue()
        {
            var concurrency = 2;
            var subscribers = 2;
            var queue = "test-ping-queue";

            MessageBusBuilder
                .Produce<PingMessage>(x => x.DefaultQueue(queue))
                .Do(builder => Enumerable.Range(0, subscribers).ToList().ForEach(i =>
                {
                    builder.SubscribeTo<PingMessage>(x => x
                        .Queue(queue)
                        .WithSubscriber<PingConsumer>()
                        .Instances(concurrency));
                }));

            await BasicPubSub(concurrency, subscribers, 1).ConfigureAwait(false);
        }

        private async Task BasicPubSub(int concurrency, int subscribers, int expectedMessageCopies)
        {
            // arrange
            var consumersCreated = new ConcurrentBag<PingConsumer>();

            MessageBusBuilder
                .WithDependencyResolver(new LookupDependencyResolver(f =>
                {
                    if (f != typeof(PingConsumer)) throw new InvalidOperationException();
                    var pingConsumer = new PingConsumer();
                    consumersCreated.Add(pingConsumer);
                    return pingConsumer;
                }));

            var messageBus = MessageBus.Value;

            // act

            // publish
            var stopwatch = Stopwatch.StartNew();

            var producedMessages = Enumerable
                .Range(0, NumberOfMessages)
                .Select(i => new PingMessage { Counter = i, Value = Guid.NewGuid() })
                .ToList();

            var messageTasks = producedMessages.Select(m => messageBus.Publish(m));
            // wait until all messages are sent
            await Task.WhenAll(messageTasks).ConfigureAwait(false);

            stopwatch.Stop();
            Log.InfoFormat(CultureInfo.InvariantCulture, "Published {0} messages in {1}", producedMessages.Count, stopwatch.Elapsed);

            // consume
            stopwatch.Restart();
            var pingConsumerConsumptionTasks = consumersCreated.Select(ConsumeAll);
            var consumersReceivedMessages = await Task.WhenAll(pingConsumerConsumptionTasks).ConfigureAwait(false);
            stopwatch.Stop();

            foreach (var receivedMessages in consumersReceivedMessages)
            {
                Log.InfoFormat(CultureInfo.InvariantCulture, "Consumed {0} messages in {1}", receivedMessages.Count, stopwatch.Elapsed);
            }

            // assert

            // ensure number of instances of consumers created matches
            consumersCreated.Count.Should().Be(subscribers * concurrency);
            consumersReceivedMessages.Length.Should().Be(subscribers * concurrency);

            // ensure all messages arrived 
            var totalReceivedMessages = consumersReceivedMessages.SelectMany(x => x).ToList();
            // ... the count should match
            totalReceivedMessages.Count.Should().Be(expectedMessageCopies * producedMessages.Count);
            // ... the content should match
            foreach (var producedMessage in producedMessages)
            {
                var messageCopies = totalReceivedMessages.Count(x => x.Counter == producedMessage.Counter && x.Value == producedMessage.Value);
                messageCopies.Should().Be(expectedMessageCopies);
            }
        }

        private static async Task<IList<PingMessage>> ConsumeAll(PingConsumer pingConsumer)
        {
            var lastMessageCount = 0;
            var lastMessageStopwatch = Stopwatch.StartNew();

            const int newMessagesAwaitingTimeout = 10;

            while (lastMessageStopwatch.Elapsed.TotalSeconds < newMessagesAwaitingTimeout)
            {
                await Task.Delay(100).ConfigureAwait(false);

                if (pingConsumer.Messages.Count != lastMessageCount)
                {
                    lastMessageCount = pingConsumer.Messages.Count;
                    lastMessageStopwatch.Restart();
                }
            }
            lastMessageStopwatch.Stop();
            return pingConsumer.Messages;
        }

        private class PingMessage
        {
            public int Counter { get; set; }
            public Guid Value { get; set; }

            #region Overrides of Object

            public override string ToString() => $"PingMessage(Counter={Counter}, Value={Value})";

            #endregion
        }

        private class PingConsumer : IConsumer<PingMessage>, IConsumerContextAware
        {
            private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            public AsyncLocal<ConsumerContext> Context { get; } = new AsyncLocal<ConsumerContext>();
            public IList<PingMessage> Messages { get; } = new List<PingMessage>();

            #region Implementation of IConsumer<in PingMessage>

            public Task OnHandle(PingMessage message, string topic)
            {
                lock (this)
                {
                    Messages.Add(message);
                }

                Log.InfoFormat(CultureInfo.InvariantCulture, "Got message {0} on topic {1}.", message.Counter, topic);
                return Task.CompletedTask;
            }

            #endregion
        }
    }
}