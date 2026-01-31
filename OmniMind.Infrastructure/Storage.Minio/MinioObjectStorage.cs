using OmniMind.Abstractions.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Storage.Minio
{
    public class MinioObjectStorage : IObjectStorage
    {
        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> GetAsync(string key, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<ObjectMetadata?> StatAsync(string key, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
