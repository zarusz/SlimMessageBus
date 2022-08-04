namespace SlimMessageBus.Host;

using System;

[Obsolete("Please use the new IConsumerWithContext interface instead")]
public interface IConsumerContextAware : IConsumerWithContext
{
}
