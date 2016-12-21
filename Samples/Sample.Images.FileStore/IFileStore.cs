using System.IO;
using System.Threading.Tasks;

namespace Sample.Images.FileStore
{
    public interface IFileStore
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns>null if file does not exist</returns>
        Task<Stream> GetFile(string id);
        void UploadFile(string id, Stream stream);
        void DeleteFile(string id);
    }
}
