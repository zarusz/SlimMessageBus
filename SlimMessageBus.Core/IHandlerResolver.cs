using System;
using System.Collections.Generic;

namespace SlimMessageBus.Core
{
    public interface IHandlerResolver
    {
        IEnumerable<IHandles<TEvent>> Resolve<TEvent>();
    }
}