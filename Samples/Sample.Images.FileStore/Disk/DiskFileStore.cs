using System;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;

namespace Sample.Images.FileStore.Disk
{
    public class DiskFileStore : IFileStore
    {
        private readonly string _folder;

        public DiskFileStore()
        {
            _folder = ConfigurationManager.AppSettings["DiskFileStorage_Folder"];
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

        public void UploadFile(string id, Stream stream)
        {
            throw new NotImplementedException();
        }

        public void DeleteFile(string id)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
