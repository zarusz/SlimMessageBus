using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    public class BaseConsumer : IDisposable
    {
        private readonly ILog _log;

        public ServiceBusMessageBus MessageBus { get; }
        public ConsumerSettings ConsumerSettings { get; }
        protected ConsumerInstancePool<Message> ConsumerInstancePool { get; }
        protected IReceiverClient Client { get; }

        public BaseConsumer(ServiceBusMessageBus messageBus, ConsumerSettings consumerSettings, IReceiverClient client, ILog log)
        {
            _log = log;
            MessageBus = messageBus;
            ConsumerSettings = consumerSettings;
            Client = client;

            ConsumerInstancePool = new ConsumerInstancePool<Message>(consumerSettings, messageBus, m => m.Body);

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
                ConsumerInstancePool.Dispose();
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
            // Process the message.
            var mf = ConsumerSettings.FormatIf(message, _log.IsDebugEnabled);
            _log.DebugFormat(CultureInfo.InvariantCulture, "Received message - {0}", mf);

            await ConsumerInstancePool.ProcessMessage(message).ConfigureAwait(false);

            if (token.IsCancellationRequested)
            {
                // Note: Use the cancellationToken passed as necessary to determine if the subscriptionClient has already been closed.
                // If subscriptionClient has already been closed, you can choose to not call CompleteAsync() or AbandonAsync() etc.
                // to avoid unnecessary exceptions.
                _log.DebugFormat(CultureInfo.InvariantCulture, "Abandon message - {0}", mf);
                await Client.AbandonAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
            }
            else
            {
                // Complete the message so that it is not received again.
                // This can be done only if the subscriptionClient is created in ReceiveMode.PeekLock mode (which is the default).
                _log.DebugFormat(CultureInfo.InvariantCulture, "Complete message - {0}", mf);
                await Client.CompleteAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
            }
        }

        // Use this handler to examine the exceptions received on the message pump.
        protected Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            try
            {
                // Execute the event hook
                (ConsumerSettings.OnMessageFault ?? MessageBus.Settings.OnMessageFault)?.Invoke(ConsumerSettings, exceptionReceivedEventArgs, exceptionReceivedEventArgs.Exception);
            }
            catch (Exception eh)
            {
                // When the hook itself error out, catch the exception
                _log.ErrorFormat(CultureInfo.InvariantCulture, "{0} method failed", eh, nameof(IConsumerEvents.OnMessageFault));
            }
            return Task.CompletedTask;
        }
    }
}