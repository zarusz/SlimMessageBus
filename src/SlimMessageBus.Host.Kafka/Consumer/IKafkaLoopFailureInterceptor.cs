namespace SlimMessageBus.Host.Kafka;

using Interceptor;

/// <summary>
/// Provides a way to intercept fatal failures occurring within the Kafka consumer group loop (<see cref="KafkaGroupConsumer.ConsumerLoop"/>).
/// These failures are typically unrecoverable errors that terminate the entire consumer group instance.
/// </summary>
public interface IKafkaLoopFailureInterceptor : IInterceptorWithOrder
{
    Task OnFailureAsync(KafkaLoopFailureContext context);
}