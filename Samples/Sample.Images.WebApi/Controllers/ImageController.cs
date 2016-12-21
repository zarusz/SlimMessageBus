using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using Sample.Images.FileStore;
using Sample.Images.Messages;
using SlimMessageBus;

namespace Sample.Images.WebApi.Controllers
{
    [AllowAnonymous]
    [RoutePrefix("api/Image")]
    public class ImageController : ApiController
    {
        private readonly IRequestResponseBus _bus;
        private readonly IFileStore _fileStore;
        private readonly IThumbnailFileIdStrategy _fileIdStrategy;

        public ImageController(IRequestResponseBus bus, IFileStore fileStore, IThumbnailFileIdStrategy fileIdStrategy)
        {
            _bus = bus;
            _fileStore = fileStore;
            _fileIdStrategy = fileIdStrategy;
        }

        [Route("{fileId}")]
        public async Task<HttpResponseMessage> GetImageThumbnail(string fileId, ThumbnailMode mode, int w, int h)
        {
            var thumbFileId = _fileIdStrategy.GetFileId(fileId, w, h, mode);

            var thumbFileContent = await _fileStore.GetFile(thumbFileId);
            if (thumbFileContent == null)
            {
                var thumbGenResponse = await _bus.Request(new GenerateThumbnailRequest(fileId, mode, w, h));

                thumbFileContent = await _fileStore.GetFile(thumbGenResponse.FileId);
            }

            return ServeStream(thumbFileContent);
        }

        [Route("{fileId}")]
        public async Task<HttpResponseMessage> GetImage(string fileId)
        {
            var fileContent = await _fileStore.GetFile(fileId);
            if (fileContent == null)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }
            return ServeStream(fileContent);
        }

        public HttpResponseMessage ServeStream(Stream content)
        {
            var r = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(content)
            };
            // ToDo: determine media type 
            r.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            // ToDo: add cache-control headers
            return r;
        }
    }
}
