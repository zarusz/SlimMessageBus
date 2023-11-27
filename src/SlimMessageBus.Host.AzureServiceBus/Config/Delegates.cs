namespace SlimMessageBus.Host.AzureServiceBus;

public delegate void AsbMessageModifier<in T>(T message, ServiceBusMessage transportMessage);
