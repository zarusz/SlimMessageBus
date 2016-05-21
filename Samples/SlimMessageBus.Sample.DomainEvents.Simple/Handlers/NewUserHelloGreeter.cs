using System;
using SlimMessageBus.Sample.DomainEvents.Simple.Events;

namespace SlimMessageBus.Sample.DomainEvents.Simple.Handlers
{
    public class NewUserHelloGreeter : IHandles<NewUserJoinedEvent>
    {
        #region Implementation of IHandles<in NewUserJoinedEvent>

        public void Handle(NewUserJoinedEvent message)
        {
            Console.WriteLine("Hello {0}", message.FullName);
        }

        #endregion
    }
}