namespace SlimMessageBus.Host.Config
{
    /// <summary>
    /// Represents a configuration strategy for the bus
    /// </summary>
    public interface IMessageBusConfigurator
    {
        /// <summary>
        /// Called during bus creation.
        /// </summary>
        /// <param name="builder">The <see cref="MessageBusBuilder"/> that can be used to configure the bus.</param>
        /// <param name="busName">The bus name (in case of the hybrid bus) that allows to determine if the configuration should be applied for that bus.</param>
        void Configure(MessageBusBuilder builder, string busName);
    }
}