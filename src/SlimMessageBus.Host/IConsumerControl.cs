namespace SlimMessageBus.Host
{
    using System.Threading.Tasks;

    public interface IConsumerControl
    {
        /// <summary>
        /// Starts message consumption
        /// </summary>
        /// <returns></returns>
        Task Start();

        /// <summary>
        /// Stops message consumption
        /// </summary>
        /// <returns></returns>
        Task Stop();
    }
}