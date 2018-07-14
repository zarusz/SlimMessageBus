using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Common.Logging;
using Sample.Images.Messages;
using SlimMessageBus;

namespace Sample.Images.Worker.Handlers
{
    public class GenerateThumbnailRequestConsumer : IConsumer<GenerateThumbnailRequest>
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly string _instanceId;

        public GenerateThumbnailRequestConsumer()
        {
            _instanceId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            Log.InfoFormat(CultureInfo.InvariantCulture, "Created instance {0}", _instanceId);
        }

        #region Implementation of ISubscriber<in GenerateThumbnailRequest>

        public Task OnHandle(GenerateThumbnailRequest message, string topic)
        {
            Log.InfoFormat(CultureInfo.InvariantCulture, "Handling message on topic {0} ({1})", topic, _instanceId);
            return Task.CompletedTask;
        }

        #endregion
    }
}