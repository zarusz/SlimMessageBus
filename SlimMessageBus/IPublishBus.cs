using System.Threading.Tasks;

namespace SlimMessageBus
{
    public interface IPublishBus
    {
        Task Publish<TMessage>(TMessage message, string topic = null);
    }
}