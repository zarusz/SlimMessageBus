namespace SlimMessageBus
{
    using System.Collections.Generic;

    /// <summary>
    /// Additional interface that indicates the Consumer wants to recieve the message headers information.
    /// </summary>
    public interface IConsumerWithHeaders
    {
        IReadOnlyDictionary<string, object> Headers { set; }
    }
}