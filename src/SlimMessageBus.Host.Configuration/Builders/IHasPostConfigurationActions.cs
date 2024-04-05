namespace SlimMessageBus.Host;

public interface IHasPostConfigurationActions
{
    /// <summary>
    /// Actions to be executed after the configuration is completed
    /// </summary>
    IList<Action<IServiceCollection>> PostConfigurationActions { get; }
}
