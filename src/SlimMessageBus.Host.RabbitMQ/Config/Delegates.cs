namespace SlimMessageBus.Host.RabbitMQ;

/// <summary>
/// Represents an initializer that is able to perform additional RabbitMQ topology setup.
/// </summary>
/// <param name="channel">The RabbitMQ client channel</param>
/// <param name="applyDefaultTopology">Calling this action will perform the default topology setup by SMB</param>
public delegate void RabbitMqTopologyInitializer(IModel channel, Action applyDefaultTopology);

/// <summary>
/// Represents a key provider for a given message.
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

/// <summary>
/// Represents an action to confirm the message.
/// </summary>
/// <param name="option"></param>
public delegate void RabbitMqMessageConfirmAction(RabbitMqMessageConfirmOptions option);

/// <summary>
/// Represents the method that handles a RabbitMQ message that includes an routing key that is obsolete or non relevent from the applicatin perspective
/// Provides access to the message, its properties, and a confirmation action.
/// </summary>
/// <remarks>Use this delegate to process messages received from RabbitMQ queues where the routing key is not
/// relevant or not provided. The handler is responsible for invoking the confirmation action to ensure proper message
/// acknowledgment.</remarks>
/// <param name="transportMessage">The event arguments containing the delivered RabbitMQ message and related metadata.</param>
public delegate RabbitMqMessageConfirmOptions RabbitMqMessageUnrecognizedRoutingKeyHandler(BasicDeliverEventArgs transportMessage);