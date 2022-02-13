namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
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
        private ServiceBusSessionProcessor serviceBusSessionProcessor;

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

            T GetSingleValue<T>(Func<AbstractConsumerSettings, T> selector, string settingName)
            {
                var set = Consumers.Select(x => selector(x.ConsumerSettings)).ToHashSet();
                if (set.Count > 1)
                {
                    throw new ConfigurationMessageBusException($"All declared consumers across the same path/subscription {TopicSubscription} must have the same {settingName} settings.");
                }
                return set.Single();
            }

            var instances = GetSingleValue(x => x.Instances, nameof(ConsumerSettings.Instances));

            var maxAutoLockRenewalDuration = GetSingleValue(x => x.GetMaxAutoLockRenewalDuration(), nameof(ConsumerBuilderExtensions.MaxAutoLockRenewalDuration))
                ?? messageBus.ProviderSettings.MaxAutoLockRenewalDuration;

            var subQueue = GetSingleValue(x => x.GetSubQueue(), nameof(ConsumerBuilderExtensions.SubQueue));

            var prefetchCount = GetSingleValue(x => x.GetPrefetchCount(), nameof(ConsumerBuilderExtensions.PrefetchCount))
                ?? messageBus.ProviderSettings.PrefetchCount;

            var enableSession = GetSingleValue(x => x.GetEnableSession(), nameof(ConsumerBuilderExtensions.EnableSession));

            var sessionIdleTimeout = GetSingleValue(x => x.GetSessionIdleTimeout(), nameof(ConsumerSessionBuilder.SessionIdleTimeout))
                ?? messageBus.ProviderSettings.SessionIdleTimeout;

            var maxConcurrentSessions = GetSingleValue(x => x.GetMaxConcurrentSessions(), nameof(ConsumerSessionBuilder.MaxConcurrentSessions))
                ?? messageBus.ProviderSettings.MaxConcurrentSessions;

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

            if (enableSession)
            {
                var options = messageBus.ProviderSettings.SessionProcessorOptionsFactory(subscriptionFactoryParams);
                options.AutoCompleteMessages = false;
                options.MaxConcurrentCallsPerSession = instances;

                if (maxAutoLockRenewalDuration != null) options.MaxAutoLockRenewalDuration = maxAutoLockRenewalDuration.Value;
                if (prefetchCount != null) options.PrefetchCount = prefetchCount.Value;
                if (sessionIdleTimeout != null) options.SessionIdleTimeout = sessionIdleTimeout.Value;
                if (maxConcurrentSessions != null) options.MaxConcurrentSessions = maxConcurrentSessions.Value;

                serviceBusSessionProcessor = messageBus.ProviderSettings.SessionProcessorFactory(subscriptionFactoryParams, options, serviceBusClient);
                serviceBusSessionProcessor.ProcessMessageAsync += ServiceBusSessionProcessor_ProcessMessageAsync;
                serviceBusSessionProcessor.ProcessErrorAsync += ServiceBusSessionProcessor_ProcessErrorAsync;
                serviceBusSessionProcessor.SessionInitializingAsync += ServiceBusSessionProcessor_SessionInitializingAsync;
                serviceBusSessionProcessor.SessionClosingAsync += ServiceBusSessionProcessor_SessionClosingAsync;
            }
            else
            {
                var options = messageBus.ProviderSettings.ProcessorOptionsFactory(subscriptionFactoryParams);
                options.AutoCompleteMessages = false;
                options.MaxConcurrentCalls = instances;

                if (maxAutoLockRenewalDuration != null) options.MaxAutoLockRenewalDuration = maxAutoLockRenewalDuration.Value;
                if (prefetchCount != null) options.PrefetchCount = prefetchCount.Value;
                if (subQueue != null) options.SubQueue = subQueue.Value;

                serviceBusProcessor = messageBus.ProviderSettings.ProcessorFactory(subscriptionFactoryParams, options, serviceBusClient);
                serviceBusProcessor.ProcessMessageAsync += ServiceBusProcessor_ProcessMessagesAsync;
                serviceBusProcessor.ProcessErrorAsync += ServiceBusProcessor_ProcessErrorAsync;
            }
        }

        public Task Start()
        {
            logger.LogInformation("Starting consumer for Path: {Path}, SubscriptionName: {SubscriptionName}", TopicSubscription.Path, TopicSubscription.SubscriptionName);

            if (serviceBusProcessor != null)
            {
                return serviceBusProcessor.StartProcessingAsync();
            }

            if (serviceBusSessionProcessor != null)
            {
                return serviceBusSessionProcessor.StartProcessingAsync();
            }
            return Task.CompletedTask;
        }

        public Task Stop()
        {
            logger.LogInformation("Stopping consumer for Path: {Path}, SubscriptionName: {SubscriptionName}", TopicSubscription.Path, TopicSubscription.SubscriptionName);
            if (serviceBusProcessor != null)
            {
                return serviceBusProcessor.StopProcessingAsync();
            }

            if (serviceBusSessionProcessor != null)
            {
                return serviceBusSessionProcessor.StopProcessingAsync();
            }
            return Task.CompletedTask;
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

            if (serviceBusSessionProcessor != null)
            {
                await serviceBusSessionProcessor.CloseAsync();
                serviceBusSessionProcessor = null;
            }

            foreach (var messageProcessor in Consumers)
            {
                await messageProcessor.DisposeSilently();
            }
            Consumers.Clear();
        }

        #endregion

        private Task ServiceBusSessionProcessor_SessionInitializingAsync(ProcessSessionEventArgs args)
        {
            logger.LogDebug("Session with id {SessionId} initializing", args.SessionId);
            return Task.CompletedTask;
        }

        private Task ServiceBusSessionProcessor_SessionClosingAsync(ProcessSessionEventArgs args)
        {
            logger.LogDebug("Session with id {SessionId} closing", args.SessionId);
            return Task.CompletedTask;
        }

        private Task ServiceBusSessionProcessor_ProcessMessageAsync(ProcessSessionMessageEventArgs args)
            => ProcessMessageAsyncInternal(args.Message, args.CompleteMessageAsync, args.AbandonMessageAsync, args.CancellationToken);

        private Task ServiceBusSessionProcessor_ProcessErrorAsync(ProcessErrorEventArgs args)
            => ProcessErrorAsyncInternal(args.Exception, args.ErrorSource);

        protected Task ServiceBusProcessor_ProcessMessagesAsync(ProcessMessageEventArgs args)
            => ProcessMessageAsyncInternal(args.Message, args.CompleteMessageAsync, args.AbandonMessageAsync, args.CancellationToken);

        protected Type GetMessageType(ServiceBusReceivedMessage message)
        {
            if (message != null && message.ApplicationProperties.TryGetValue(MessageHeaders.MessageType, out var messageTypeValue) && messageTypeValue is string messageTypeName)
            {
                var messageType = MessageBus.Settings.MessageTypeResolver.ToType(messageTypeName);
                return messageType;
            }
            return null;
        }

        protected Task ServiceBusProcessor_ProcessErrorAsync(ProcessErrorEventArgs args)
            => ProcessErrorAsyncInternal(args.Exception, args.ErrorSource);

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

        protected async Task ProcessMessageAsyncInternal(ServiceBusReceivedMessage message, Func<ServiceBusReceivedMessage, CancellationToken, Task> completeMessage, Func<ServiceBusReceivedMessage, IDictionary<string, object>, CancellationToken, Task> abandonMessage, CancellationToken token)
        {
            var messageType = GetMessageType(message);
            var consumerInvoker = TryMatchConsumer(messageType);

            // Process the message.
            logger.LogDebug("Received message - Path: {Path}, SubscriptionName: {SubscriptionName}, SequenceNumber: {SequenceNumber}, DeliveryCount: {DeliveryCount}, MessageId: {MessageId}", TopicSubscription.Path, TopicSubscription.SubscriptionName, message.SequenceNumber, message.DeliveryCount, message.MessageId);

            if (token.IsCancellationRequested)
            {
                // Note: Use the cancellationToken passed as necessary to determine if the subscriptionClient has already been closed.
                // If subscriptionClient has already been closed, you can choose to not call CompleteAsync() or AbandonAsync() etc.
                // to avoid unnecessary exceptions.
                logger.LogDebug("Abandon message - Path: {Path}, SubscriptionName: {SubscriptionName}, SequenceNumber: {SequenceNumber}, DeliveryCount: {DeliveryCount}, MessageId: {MessageId}", TopicSubscription.Path, TopicSubscription.SubscriptionName, message.SequenceNumber, message.DeliveryCount, message.MessageId);
                await abandonMessage(message, null, token).ConfigureAwait(false);

                return;
            }

            var exception = await consumerInvoker.Processor.ProcessMessage(message, consumerInvoker.Invoker).ConfigureAwait(false);
            if (exception != null)
            {
                logger.LogError(exception, "Abandon message (exception occured while processing) - Path: {Path}, SubscriptionName: {SubscriptionName}, SequenceNumber: {SequenceNumber}, DeliveryCount: {DeliveryCount}, MessageId: {MessageId}", TopicSubscription.Path, TopicSubscription.SubscriptionName, message.SequenceNumber, message.DeliveryCount, message.MessageId);

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
                await abandonMessage(message, messageProperties, token).ConfigureAwait(false);

                return;
            }

            // Complete the message so that it is not received again.
            // This can be done only if the subscriptionClient is created in ReceiveMode.PeekLock mode (which is the default).
            logger.LogDebug("Complete message - Path: {Path}, SubscriptionName: {SubscriptionName}, SequenceNumber: {SequenceNumber}, DeliveryCount: {DeliveryCount}, MessageId: {MessageId}", TopicSubscription.Path, TopicSubscription.SubscriptionName, message.SequenceNumber, message.DeliveryCount, message.MessageId);
            await completeMessage(message, token).ConfigureAwait(false);
        }

        protected Task ProcessErrorAsyncInternal(Exception exception, ServiceBusErrorSource errorSource)
        {
            try
            {
                logger.LogError(exception, "Error while processing Path: {Path}, SubscriptionName: {SubscriptionName}, Error Message: {ErrorMessage}, Error Source: {ErrorSource}", TopicSubscription.Path, TopicSubscription.SubscriptionName, exception.Message, errorSource);

                // Execute the event hook
                MessageBus.Settings.OnMessageFault?.Invoke(MessageBus, null, null, exception, null);
            }
            catch (Exception eh)
            {
                MessageBusBase.HookFailed(logger, eh, nameof(IConsumerEvents.OnMessageFault));
            }
            return Task.CompletedTask;
        }
    }
}