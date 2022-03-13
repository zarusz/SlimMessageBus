namespace SlimMessageBus.Host.Collections
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Collection of producer settings indexed by message type (including base types).
    /// The message type hierarchy is discovered at runtime and cached for faster access.
    /// </summary>
    /// <typeparam name="TProducer">The producer type</typeparam>
    public class ProducerByMessageTypeCache<TProducer> where TProducer : class
    {
        private readonly ILogger logger;
        private readonly IDictionary<Type, TProducer> producerByBaseType;
        private readonly SafeDictionaryWrapper<Type, TProducer> producerByType;

        public ProducerByMessageTypeCache(ILogger logger, IDictionary<Type, TProducer> producerByBaseType)
        {
            this.logger = logger;
            this.producerByBaseType = producerByBaseType;
            this.producerByType = new SafeDictionaryWrapper<Type, TProducer>();
        }

        public TProducer this[Type key] => GetProducer(key);

        /// <summary>
        /// Find the nearest base type (or exact type) that has the producer declared (from the dictionary).
        /// </summary>
        /// <param name="messageType"></param>
        /// <returns>Producer when found, else null</returns>
        public TProducer GetProducer(Type messageType)
            => producerByType.GetOrAdd(messageType, CalculateProducer);

        private TProducer CalculateProducer(Type messageType)
        {
            var baseType = messageType;
            while (baseType != null && !producerByBaseType.ContainsKey(baseType))
            {
                baseType = baseType.BaseType;
            }

            if (baseType != null && producerByBaseType.TryGetValue(baseType, out var producer))
            {
                if (baseType != messageType)
                {
                    logger.LogDebug("Found matching base message type {BaseMessageType} producer for message type {MessageType}", baseType, messageType);
                }
                else
                {
                    logger.LogDebug("Using declared producer for message type {MessageType}", messageType);
                }
                return producer;
            }

            logger.LogDebug("Did not find any declared producer with message type (or base message type) matching {MessageType}", messageType);

            // Note: Nulls are also added to dictionary, so that we don't look them up using reflection next time (cached).
            return null;
        }
    }
}
