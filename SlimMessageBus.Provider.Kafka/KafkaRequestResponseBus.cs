using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SlimMessageBus.Provider.Kafka
{

    public class InboundTopicConfig
    {
        public string Topic { get; set; }
        public bool IsRequestResponse { get; set; }
        public int MaxThreads { get; set; }
    }

    public class MessageBusConfiguration
    {
        public IDictionary<Type, string> OutboundTopicByMessageType { get; set; }
    }


    public interface IMessageRouter
    {
        string GetTopic(Type messageType);
    }



    public interface IBusTransport
    {
        Task Publish(string topic, byte[] payload);
    }
}