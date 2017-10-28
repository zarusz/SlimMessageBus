using System.Threading.Tasks;

namespace SlimMessageBus
{
    public interface IMessageBusSubscription
    {
        Task Subscribe<TMessage>(string topic = null);
        Task UnSubscribe<TMessage>(string topic = null);
    }
}