using System.Threading.Tasks;

namespace SlimMessageBus
{
    /// <summary>
    /// Subscriber of given message types
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    public interface ISubscriber<in TMessage>
    {
        Task OnHandle(TMessage message, string topic);
    }
}