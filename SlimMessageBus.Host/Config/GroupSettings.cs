using System.Collections.Generic;

namespace SlimMessageBus.Config
{
    public class GroupSettings
    {
        public string GroupId { get; set; }
        public IList<SubscriberSettings> SubscriberSettings { get; set; }
    }
}