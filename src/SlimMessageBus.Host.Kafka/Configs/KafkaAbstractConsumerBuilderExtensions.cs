namespace SlimMessageBus.Host.Kafka;

using System.Diagnostics.CodeAnalysis;

public static class KafkaAbstractConsumerBuilderExtensions
{
    /// <summary>
    /// Configures the Kafka consumer group.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="group"></param>
    /// <returns></returns>
    [Obsolete("Use KafkaGroup() instead")]
    [ExcludeFromCodeCoverage]
    public static T Group<T>(this T builder, string group)
        where T : IAbstractConsumerBuilder
        => builder.KafkaGroup<T>(group);

    /// <summary>
    /// Configures the Kafka consumer group.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="group"></param>
    /// <returns></returns>
    [Obsolete("Use KafkaGroup() instead")]
    [ExcludeFromCodeCoverage]
    public static RequestResponseBuilder Group(this RequestResponseBuilder builder, string group)
        => builder.KafkaGroup(group);

    /// <summary>
    /// Configures the Kafka consumer group.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="group"></param>
    /// <returns></returns>
    public static T KafkaGroup<T>(this T builder, string group)
        where T : IAbstractConsumerBuilder
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder.ConsumerSettings.SetGroup(group);
        return builder;
    }

    /// <summary>
    /// Checkpoint every N-th processed message.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="numberOfMessages"></param>
    /// <returns></returns>
    public static T CheckpointEvery<T>(this T builder, int numberOfMessages)
        where T : IAbstractConsumerBuilder
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder.ConsumerSettings.Properties[CheckpointSettings.CheckpointCount] = numberOfMessages;
        return builder;
    }

    /// <summary>
    /// Checkpoint after T elapsed time.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="duration"></param>
    /// <returns></returns>
    public static T CheckpointAfter<T>(this T builder, TimeSpan duration)
        where T : IAbstractConsumerBuilder
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        builder.ConsumerSettings.Properties[CheckpointSettings.CheckpointDuration] = duration;
        return builder;
    }
}
