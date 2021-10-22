namespace SlimMessageBus.Host.KubeMQ
{
    using System;

    public class KubeMQMessageBusSettings
    {
        /// <summary>
        /// The client id.
        /// </summary>
        public string ClientID { get; set; }
        /// <summary>
        /// The KubeMQ server address.
        /// </summary>
        public string ServerAddress { get; set; }

        public KubeMQMessageBusSettings(string brokerList, string clientId)
        {
            ServerAddress = brokerList;
            ClientID = clientId;
        }
    }
}