namespace SlimMessageBus.Host.Config
{
    using System;

    /// <summary>
    /// The request/response settings.
    /// </summary>
    public class RequestResponseSettings : AbstractConsumerSettings
    {
        /// <summary>
        /// Default wait time for the response to arrive. This is used when the timeout during request send method was not provided.
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Called whenever an incoming response message errors out.
        /// </summary>
        public Action<RequestResponseSettings, object, Exception> OnResponseMessageFault { get; set; }

        public RequestResponseSettings()
        {
            Timeout = TimeSpan.FromSeconds(20);
        }
    }
}