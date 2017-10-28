namespace SlimMessageBus.Sample.DomainEvents.Simple.Events
{
    public class NewUserJoinedEvent
    {
        public string FullName { get; protected set; }

        public NewUserJoinedEvent(string fullName)
        {
            FullName = fullName;
        }
    }
}