using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.Logging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    public class BaseConsumer : IDisposable
    {
        private readonly ILogger _logger;

        public ServiceBusMessageBus MessageBus { get; }
        public AbstractConsumerSettings ConsumerSettings { get; }
        protected IReceiverClient Client { get; }
        protected IMessageProcessor<Message> MessageProcessor { get; }

        public BaseConsumer(ServiceBusMessageBus messageBus, AbstractConsumerSettings consumerSettings, IReceiverClient client, IMessageProcessor<Message> messageProcessor, ILogger logger)
        {
            MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            ConsumerSettings = consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings));
            Client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger;

            MessageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));

            // Configure the message handler options in terms of exception handling, number of concurrent messages to deliver, etc.
            var messageHandlerOptions = new MessageHandlerOptions(ExceptionReceivedHandler)
            {
                // Maximum number of concurrent calls to the callback ProcessMessagesAsync(), set to 1 for simplicity.
                // Set it according to how many messages the application wants to process in parallel.
                MaxConcurrentCalls = consumerSettings.Instances,

                // Indicates whether the message pump should automatically complete the messages after returning from user callback.
                // False below indicates the complete operation is handled by the user callback as in ProcessMessagesAsync().
                AutoComplete = false
            };

            // Register the function that processes messages.
            Client.RegisterMessageHandler(ProcessMessagesAsync, messageHandlerOptions);
        }

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                MessageProcessor.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        protected async Task ProcessMessagesAsync(Message message, CancellationToken token)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));

            // Process the message.
            var mf = ConsumerSettings.FormatIf(message, _logger.IsEnabled(LogLevel.Debug));
            _logger.LogDebug("Received message - {0}", mf);

            if (token.IsCancellationRequested)
            {
                // Note: Use the cancellationToken passed as necessary to determine if the subscriptionClient has already been closed.
                // If subscriptionClient has already been closed, you can choose to not call CompleteAsync() or AbandonAsync() etc.
                // to avoid unnecessary exceptions.
                _logger.LogDebug("Abandon message - {0}", mf);
                await Client.AbandonAsync(message.SystemProperties.LockToken).ConfigureAwait(false);

                return;
            }

            var exception = await MessageProcessor.ProcessMessage(message).ConfigureAwait(false);
            if (exception != null)
            {
                if (mf == null)
                {
                    mf = ConsumerSettings.FormatIf(message, true);
                }
                _logger.LogError(exception, "Abandon message (exception occured while processing) - {0}", mf);

                var messageProperties = new Dictionary<string, object>
                {
                    // Set the exception message
                    ["SMB.Exception"] = exception.Message
                };
                await Client.AbandonAsync(message.SystemProperties.LockToken, messageProperties).ConfigureAwait(false);

                return;
            }

            // Complete the message so that it is not received again.
            // This can be done only if the subscriptionClient is created in ReceiveMode.PeekLock mode (which is the default).
            _logger.LogDebug("Complete message - {0}", mf);
            await Client.CompleteAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
        }

        // Use this handler to examine the exceptions received on the message pump.
        protected Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            try
            {
                // Execute the event hook
                ConsumerSettings.OnMessageFault?.Invoke(MessageBus, ConsumerSettings, exceptionReceivedEventArgs, exceptionReceivedEventArgs.Exception);
                MessageBus.Settings.OnMessageFault?.Invoke(MessageBus, ConsumerSettings, exceptionReceivedEventArgs, exceptionReceivedEventArgs.Exception);
            }
            catch (Exception eh)
            {
                MessageBusBase.HookFailed(_logger, eh, nameof(IConsumerEvents.OnMessageFault));
            }
            return Task.CompletedTask;
        }
    }
}