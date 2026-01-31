using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Abstractions.Storage
{
    /// <summary>
    /// （MinIO/OSS）
    /// </summary>
    public interface IObjectStorage
    {
        Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default);
        Task<Stream> GetAsync(string key, CancellationToken ct = default);
        Task<bool> ExistsAsync(string key, CancellationToken ct = default);
        Task DeleteAsync(string key, CancellationToken ct = default);
        Task<ObjectMetadata?> StatAsync(string key, CancellationToken ct = default);
        Task<string> GetPresignedUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct = default);
    }
    public sealed record ObjectMetadata(string Key, long Size, string? ContentType, string? ETag);
}
