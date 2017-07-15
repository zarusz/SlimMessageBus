using System;

namespace SlimMessageBus.Host.Config
{
    /// <summary>
    /// The request/response settings.
    /// </summary>
    public class RequestResponseSettings : ITopicGroupConsumerSettings, IConsumerEvents
    {
        /// <summary>
        /// Individual topic that will act as a the private reply queue for the app domain.
        /// </summary>
        public string Topic { get; set; }
        /// <summary>
        /// Consummer GroupId to to use for the app domain.
        /// </summary>
        public string Group { get; set; }
        /// <summary>
        /// Default wait time for the response to arrive. This is used when the timeout during publish was not provided.
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

        public RequestResponseSettings()
        {
            Timeout = TimeSpan.FromSeconds(20);
        }
    }
}