using System;

namespace SlimMessageBus.Host.Config
{
    /// <summary>
    /// The request/response settings.
    /// </summary>
    public class RequestResponseSettings : HasProviderExtensions, ITopicGroupConsumerSettings, IConsumerEvents
    {
        /// <summary>
        /// Topic that will act as a the private reply queue for the application.
        /// </summary>
        public string Topic { get; set; }
        /// <summary>
        /// Consummer GroupId to to use.
        /// </summary>
        public string Group { get; set; }
        /// <summary>
        /// Default wait time for the response to arrive. This is used when the timeout during request send method was not provided.
        /// </summary>
        public TimeSpan Timeout { get; set; }

        #region Implementation of IConsumerEvents

        /// <summary>
        /// Called whenever a consumer receives an expired message.
        /// </summary>
        public Action<ConsumerSettings, object> OnMessageExpired { get; set; }
        /// <summary>
        /// Called whenever a consumer errors out while processing the message.
        /// </summary>
        public Action<ConsumerSettings, object, Exception> OnMessageFault { get; set; }

        #endregion

        /// <summary>
        /// Called whenever an incomming response message errors out.
        /// </summary>
        public Action<RequestResponseSettings, object, Exception> OnResponseMessageFault { get; set; }

        public RequestResponseSettings()
        {
            Timeout = TimeSpan.FromSeconds(20);
        }
    }
}