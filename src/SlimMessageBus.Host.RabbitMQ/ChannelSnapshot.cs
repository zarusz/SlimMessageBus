namespace SlimMessageBus.Host.RabbitMQ;

using global::RabbitMQ.Client;

/// <summary>
/// An immutable snapshot of the current RabbitMQ channels, captured atomically to avoid
/// time-of-check-time-of-use (TOCTOU) races between channel availability checks and publish operations.
/// </summary>
internal readonly struct ChannelSnapshot
{
    /// <summary>
    /// The regular (fire-and-forget) channel.
    /// </summary>
    public IChannel Channel { get; }

    /// <summary>
    /// The channel with publisher confirms enabled, or <c>null</c> if no producer requires confirms.
    /// </summary>
    public IChannel ConfirmsChannel { get; }

    public ChannelSnapshot(IChannel channel, IChannel confirmsChannel)
    {
        Channel = channel;
        ConfirmsChannel = confirmsChannel;
    }
}
