namespace SlimMessageBus.Config
{
    public class GroupBuilder
    {
        private readonly GroupSettings _settings;

        public GroupBuilder(GroupSettings settings)
        {
            _settings = settings;
        }

        public GroupBuilder Id(string groupId)
        {
            _settings.GroupId = groupId;
            return this;
        }
    }
}
