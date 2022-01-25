namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;

    public abstract class AsbBaseConsumer : IAsyncDisposable, IConsumerControl
    {
        private readonly ILogger logger;
        public ServiceBusMessageBus MessageBus { get; }
        protected IList<IMessageProcessor<ServiceBusReceivedMessage>> Consumers { get; }
        protected IDictionary<Type, ConsumerInvoker> InvokerByMessageType { get; }
        protected ConsumerInvoker SingleInvoker { get; }
        protected TopicSubscriptionParams TopicSubscription { get; }

        private ServiceBusProcessor serviceBusProcessor;

        protected AsbBaseConsumer(ServiceBusMessageBus messageBus, ServiceBusClient serviceBusClient, TopicSubscriptionParams subscriptionFactoryParams, IEnumerable<IMessageProcessor<ServiceBusReceivedMessage>> consumers, ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            TopicSubscription = subscriptionFactoryParams ?? throw new ArgumentNullException(nameof(consumers)); ;
            Consumers = consumers?.ToList() ?? throw new ArgumentNullException(nameof(consumers));

            if (Consumers.Count == 0)
            {
                throw new InvalidOperationException($"The {nameof(consumers)} needs to be non empty");
            }

            var instances = Consumers.First().ConsumerSettings.Instances;
            if (Consumers.Any(x => x.ConsumerSettings.Instances != instances))
            {
                throw new ConfigurationMessageBusException($"All declared consumers across the same path/subscription {TopicSubscription} must have the same {nameof(ConsumerSettings.Instances)} settings.");
            }

            InvokerByMessageType = Consumers
                .Where(x => x.ConsumerSettings is ConsumerSettings)
                .Select(x => (Processor: x, ConsumerSettings: (ConsumerSettings)x.ConsumerSettings))
                .Where(x => string.Equals(x.ConsumerSettings.GetSubscriptionName(required: false), TopicSubscription.SubscriptionName))
                .SelectMany(x => x.ConsumerSettings.ConsumersByMessageType.Values.Select(invoker => new ConsumerInvoker(x.Processor, invoker)))
                .ToDictionary(x => x.Invoker.MessageType);

            var responseInvoker = Consumers
                    .Where(x => x.ConsumerSettings is RequestResponseSettings)
                    .Select(x => (Processor: x, RequestResponseSettings: (RequestResponseSettings)x.ConsumerSettings))
                    .Select(x => new ConsumerInvoker(x.Processor, null))
                    .FirstOrDefault();

            SingleInvoker = InvokerByMessageType.Count == 1
                ? InvokerByMessageType.First().Value
                : responseInvoker;

            var options = new ServiceBusProcessorOptions
            {
                // Maximum number of concurrent calls to the callback ProcessMessagesAsync(), set to 1 for simplicity.
                // Set it according to how many messages the application wants to process in parallel.
                MaxConcurrentCalls = instances,

                // Indicates whether the message pump should automatically complete the messages after returning from user callback.
                // False below indicates the complete operation is handled by the user callback as in ProcessMessagesAsync().
                AutoCompleteMessages = false,

                // ToDo: Add more config optons
                /*
                MaxAutoLockRenewalDuration = 
                PrefetchCount = 
                ReceiveMode = ServiceBusReceiveMode.
                SubQueue = 
                */
            };

            serviceBusProcessor = messageBus.ProviderSettings.ProcessorFactory(subscriptionFactoryParams, options, serviceBusClient);
            serviceBusProcessor.ProcessMessageAsync += ProcessMessagesAsync;
            serviceBusProcessor.ProcessErrorAsync += ExceptionReceivedHandler;
        }

        public Task Start()
        {
            logger.LogInformation("Starting consumer for Path: {Path}, SubscriptionName: {SubscriptionName}", TopicSubscription.Path, TopicSubscription.SubscriptionName);
            return serviceBusProcessor.StartProcessingAsync();
        }

        public Task Stop()
        {
            logger.LogInformation("Stopping consumer for Path: {Path}, SubscriptionName: {SubscriptionName}", TopicSubscription.Path, TopicSubscription.SubscriptionName);
            return serviceBusProcessor.StopProcessingAsync();
        }

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (serviceBusProcessor != null)
            {
                await serviceBusProcessor.CloseAsync();
                serviceBusProcessor = null;
            }

            foreach (var messageProcessor in Consumers)
            {
                await messageProcessor.DisposeSilently();
            }
            Consumers.Clear();
        }

        #endregion

        protected virtual ConsumerInvoker TryMatchConsumer(Type messageType)
        {
            if (messageType == null && SingleInvoker == null)
            {
                throw new MessageBusException($"The message arrived without {MessageHeaders.MessageType} header on path {TopicSubscription}, so it is imposible to match one of the known consumer types {string.Join(",", InvokerByMessageType.Values.Select(x => x.Invoker.ConsumerType.Name))}");
            }

            if (messageType != null && InvokerByMessageType.Count > 0)
            {
                // Find proper Consumer from Consumers based on the incoming message type
                do
                {
                    if (InvokerByMessageType.TryGetValue(messageType, out var consumerInvoker))
                    {
                        return consumerInvoker;
                    }
                    messageType = messageType.BaseType;
                }
                while (messageType != typeof(object));
            }

            // fallback to the first one
            return SingleInvoker;
        }

        protected async Task ProcessMessagesAsync(ProcessMessageEventArgs args)
        {
            if (args is null) throw new ArgumentNullException(nameof(args));

            var message = args.Message;
            var messageType = GetMessageType(args.Message);
            var consumerInvoker = TryMatchConsumer(messageType);

            // Process the message.
            var mf = consumerInvoker.Processor.ConsumerSettings.FormatIf(args.Message, logger.IsEnabled(LogLevel.Debug));
            logger.LogDebug("Received message - {0}", mf);

            if (args.CancellationToken.IsCancellationRequested)
            {
                // Note: Use the cancellationToken passed as necessary to determine if the subscriptionClient has already been closed.
                // If subscriptionClient has already been closed, you can choose to not call CompleteAsync() or AbandonAsync() etc.
                // to avoid unnecessary exceptions.
                logger.LogDebug("Abandon message - {0}", mf);
                await args.AbandonMessageAsync(message).ConfigureAwait(false);

                return;
            }

            var exception = await consumerInvoker.Processor.ProcessMessage(message, consumerInvoker.Invoker).ConfigureAwait(false);
            if (exception != null)
            {
                if (mf == null)
                {
                    mf = consumerInvoker.Processor.ConsumerSettings.FormatIf(message, true);
                }
                logger.LogError(exception, "Abandon message (exception occured while processing) - {0}", mf);

                try
                {
                    // Execute the event hook
                    consumerInvoker.Processor.ConsumerSettings.OnMessageFault?.Invoke(MessageBus, consumerInvoker.Processor.ConsumerSettings, null, exception, message);
                    MessageBus.Settings.OnMessageFault?.Invoke(MessageBus, consumerInvoker.Processor.ConsumerSettings, null, exception, message);
                }
                catch (Exception eh)
                {
                    MessageBusBase.HookFailed(logger, eh, nameof(IConsumerEvents.OnMessageFault));
                }

                var messageProperties = new Dictionary<string, object>
                {
                    // Set the exception message
                    ["SMB.Exception"] = exception.Message
                };
                await args.AbandonMessageAsync(message, propertiesToModify: messageProperties).ConfigureAwait(false);

                return;
            }

            // Complete the message so that it is not received again.
            // This can be done only if the subscriptionClient is created in ReceiveMode.PeekLock mode (which is the default).
            logger.LogDebug("Complete message - {0}", mf);
            await args.CompleteMessageAsync(message).ConfigureAwait(false);
        }

        protected Type GetMessageType(ServiceBusReceivedMessage message)
        {
            if (message != null && message.ApplicationProperties.TryGetValue(MessageHeaders.MessageType, out var messageTypeValue) && messageTypeValue is string messageTypeName)
            {
                var messageType = MessageBus.Settings.MessageTypeResolver.ToType(messageTypeName);
                return messageType;
            }
            return null;
        }

        // Use this handler to examine the exceptions received on the message pump.
        protected Task ExceptionReceivedHandler(ProcessErrorEventArgs args)
        {
            try
            {
                logger.LogError(args.Exception, "Error while processing Path: {Path}, SubscriptionName: {SubscriptionName}, Error: {Error}", TopicSubscription.Path, TopicSubscription.SubscriptionName, args.Exception.Message);

                // Execute the event hook
                MessageBus.Settings.OnMessageFault?.Invoke(MessageBus, null, null, args?.Exception, null);
            }
            catch (Exception eh)
            {
                MessageBusBase.HookFailed(logger, eh, nameof(IConsumerEvents.OnMessageFault));
            }
            return Task.CompletedTask;
        }
    }
}