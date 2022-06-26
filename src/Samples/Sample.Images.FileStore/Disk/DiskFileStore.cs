namespace Sample.Images.FileStore.Disk;

using System;
using System.IO;
using System.Threading.Tasks;

public class DiskFileStore : IFileStore
{
    private readonly string _folder;

    public DiskFileStore(string folder)
    {
        _folder = folder;
    }

    #region Implementation of IFileStore

    public Task<Stream> GetFile(string id)
    {
        var filePath = Path.Combine(_folder, id);
        try
        {
            var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult<Stream>(fileStream);
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult<Stream>(null);
        }
        catch (Exception e)
        {
            return Task.FromException<Stream>(e);
        }
    }

    public Task UploadFile(string id, Stream stream)
    {
        var filePath = Path.Combine(_folder, id);
        try
        {
            using (var fileStream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                stream.CopyTo(fileStream);
            }
            return Task.FromResult(0);
        }
        catch (Exception e)
        {
            return Task.FromException(e);
        }
    }

    public void DeleteFile(string id)
    {
        var filePath = Path.Combine(_folder, id);
        File.Delete(filePath);
    }

    #endregion
}
