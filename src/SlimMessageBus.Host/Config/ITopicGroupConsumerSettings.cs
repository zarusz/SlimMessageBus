namespace SlimMessageBus.Host.Config
{
    public interface ITopicGroupConsumerSettings
    {
        /// <summary>
        /// Individual topic that will act as a the private reply queue for the app domain.
        /// </summary>
        string Topic { get; }
        /// <summary>
        /// Consummer GroupId to to use for the app domain.
        /// </summary>
        string Group { get; }
    }
}