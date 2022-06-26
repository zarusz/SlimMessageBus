namespace Sample.Images.FileStore;

using System.IO;
using System.Threading.Tasks;

public interface IFileStore
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <returns>null if file does not exist</returns>
    Task<Stream> GetFile(string id);
    Task UploadFile(string id, Stream stream);
    void DeleteFile(string id);
}
