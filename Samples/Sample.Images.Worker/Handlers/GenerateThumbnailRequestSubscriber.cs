using System;
using System.Threading.Tasks;
using Common.Logging;
using Sample.Images.Messages;
using SlimMessageBus;

namespace Sample.Images.Worker.Handlers
{
    public class GenerateThumbnailRequestConsumer : IConsumer<GenerateThumbnailRequest>
    {
        private static readonly ILog Log = LogManager.GetLogger<GenerateThumbnailRequestConsumer>();

        private readonly string _instanceId;

        public GenerateThumbnailRequestConsumer()
        {
            _instanceId = Guid.NewGuid().ToString("N");
            Log.InfoFormat("Created instance {0}", _instanceId);
        }

        #region Implementation of ISubscriber<in GenerateThumbnailRequest>

        public Task OnHandle(GenerateThumbnailRequest message, string topic)
        {
            Log.InfoFormat("Handling message on topic {0} ({1})", topic, _instanceId);
            return Task.FromResult(false);
        }

        #endregion
    }
}