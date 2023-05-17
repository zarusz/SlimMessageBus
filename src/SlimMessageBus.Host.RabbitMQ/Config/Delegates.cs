namespace SlimMessageBus.Host.RabbitMQ;

/// <summary>
/// Represents an initializer that is able to perform additional RabbitMQ topology setup.
/// </summary>
/// <param name="channel">The RabbitMQ client channel</param>
/// <param name="applyDefaultTopology">Calling this action will peform the default topology setup by SMB</param>
public delegate void RabbitMqTopologyInitializer(IModel channel, Action applyDefaultTopology);

/// <summary>
/// Represents a key provider provider for a given message.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="message">The message</param>
/// <param name="messageProperties">The message properties</param>
/// <returns></returns>
public delegate string RabbitMqMessageRoutingKeyProvider<T>(T message, IBasicProperties messageProperties);

/// <summary>
/// Represents a message modifier for a given message.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="message">The message</param>
/// <param name="messageProperties">The message properties to be modified</param>
/// <returns></returns>
public delegate void RabbitMqMessagePropertiesModifier<T>(T message, IBasicProperties messageProperties);

