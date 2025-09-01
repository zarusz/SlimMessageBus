namespace SlimMessageBus.Host.AmazonSQS;

internal record struct SqsTransportMessageWithPayload(Message TransportMessage, string Payload);
