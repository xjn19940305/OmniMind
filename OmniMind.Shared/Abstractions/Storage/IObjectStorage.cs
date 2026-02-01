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
        /// <summary>
        /// 上传对象
        /// </summary>
        /// <param name="key"></param>
        /// <param name="content"></param>
        /// <param name="contentType"></param>
        /// <param name="metadata">自定义元数据，会以 X-Amz-Meta- 前缀存储</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task PutAsync(string key, Stream content, string contentType, Dictionary<string, string>? metadata = null, CancellationToken ct = default);

        /// <summary>
        /// 获取对象
        /// </summary>
        /// <param name="key"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<Stream> GetAsync(string key, CancellationToken ct = default);
        /// <summary>
        /// 判断是否存在
        /// </summary>
        /// <param name="key"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<bool> ExistsAsync(string key, CancellationToken ct = default);
        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="key"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task DeleteAsync(string key, CancellationToken ct = default);
        /// <summary>
        /// 判读文件状态
        /// </summary>
        /// <param name="key"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<ObjectMetadata?> StatAsync(string key, CancellationToken ct = default);
        /// <summary>
        /// 生成租户对象路径
        /// </summary>
        /// <param name="key"></param>
        /// <param name="expiresIn"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<string> GetPresignedUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct = default);
    }
    public sealed record ObjectMetadata(string Key, long Size, string? ContentType, string? ETag);
}
