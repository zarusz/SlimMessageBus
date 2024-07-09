namespace SlimMessageBus.Host.RabbitMQ;

/// <summary>
/// Specifies how messages are confirmed with RabbitMq
/// </summary>
public enum RabbitMqMessageAcknowledgementMode
{
    /// <summary>
    /// Each message will get an Ack after successful processing, or when error happens the message will get an Nack in the end.
    /// However, if the user made any manual ConsumerContext.Ack() or ConsumerContext.Nack() during the consumption process (or in an interceptor), that will be used to confirm the message instead.
    /// This results in at-least-once delivery guarantee and a safe processing.
    /// </summary>
    /// <remarks>That is the default option</remarks>
    ConfirmAfterMessageProcessingWhenNoManualConfirmMade = 0,

    /// <summary>    
    /// The message will already be considered as Ack upon receive. See https://www.rabbitmq.com/docs/confirms#acknowledgement-modes for details.
    /// This results in at-most-once delivery guarantee (messages could be lost if processing would not fully finish).
    /// This is managed by the protocol and should give faster throughput than <see cref="RabbitMqMessageAcknowledgementMode.AckMessageBeforeProcessing"/> while leading to same delivery guarantees.
    /// </summary>
    AckAutomaticByRabbit = 1,

    /// <summary>
    /// The message will be Ack-ed by SMB before the actual message processing starts.
    /// This results in at-most-once delivery guarantee (messages could be lost if processing would not fully finish).
    /// </summary>
    AckMessageBeforeProcessing = 2,
}