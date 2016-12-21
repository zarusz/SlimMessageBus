namespace SlimMessageBus.Config
{
    public class PublisherBuilder<T>
    {
        private readonly PublisherSettings _settings;

        public PublisherBuilder(PublisherSettings settings)
        {
            _settings = settings;
            _settings.MessageType = typeof (T);
        }

        public PublisherBuilder<T> OnTopicByDefault(string name)
        {
            _settings.DefaultTopic = name;
            return this;
        }
    }
}