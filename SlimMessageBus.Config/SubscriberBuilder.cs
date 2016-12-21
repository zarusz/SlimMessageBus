namespace SlimMessageBus.Config
{
    public class SubscriberBuilder<T>
    {
        private readonly SubscriberSettings _settings;

        public SubscriberBuilder(SubscriberSettings settings)
        {
            _settings = settings;
            _settings.MessageType = typeof (T);
        }

        public SubscriberBuilder<T> OnTopic(string name)
        {
            _settings.Topic = name;
            return this;
        }
    }
}