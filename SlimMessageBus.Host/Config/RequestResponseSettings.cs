using System;

namespace SlimMessageBus.Host.Config
{
    /// <summary>
    /// The request/response settings.
    /// </summary>
    public class RequestResponseSettings
    {
        /// <summary>
        /// Default wait time for the response to come in. This is used when the timeout during publish was not provided.
        /// </summary>
        public TimeSpan Timeout { get; set; }
        /// <summary>
        /// Individual topic that will act as a the private reply queue for the app domain.
        /// </summary>
        public string Topic { get; set; }
        /// <summary>
        /// Consummer GroupId to to use for the app domain.
        /// </summary>
        public string Group { get; set; }
        /// <summary>
        /// Dedicated <see cref="IMessageSerializer"/> capable of serializing <see cref="MessageWithHeaders"/>.
        /// By default uses <see cref="MessageWithHeadersSerializer"/>.
        /// </summary>
        public IMessageSerializer MessageWithHeadersSerializer { get; set; }

        public RequestResponseSettings()
        {
            MessageWithHeadersSerializer = new MessageWithHeadersSerializer();
        }
    }
}