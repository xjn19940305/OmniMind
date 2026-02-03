using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using OmniMind.Abstractions.Storage;
using OmniMind.Storage.Minio;
using System.Text;

namespace OmniMind.Storage.Minio
{
    public class MinioObjectStorage : IObjectStorage
    {
        private readonly IMinioClient client;
        private readonly OssOptions options;
        private readonly string bucketName;

        public MinioObjectStorage(
            IMinioClient client,
            IOptions<OssOptions> options
            )
        {
            this.client = client;
            this.options = options.Value;
            this.bucketName = this.options.Bucket ?? "omnimind";
        }

        private async Task EnsureBucketExistsAsync(CancellationToken ct = default)
        {
            var beArgs = new BucketExistsArgs()
                .WithBucket(bucketName);
            var found = await client.BucketExistsAsync(beArgs, ct);
            if (!found)
            {
                var mbArgs = new MakeBucketArgs()
                    .WithBucket(bucketName);
                await client.MakeBucketAsync(mbArgs, ct);
            }
        }

        /// <summary>
        /// 上传对象（带自定义元数据）
        /// </summary>
        public async Task PutAsync(string key, Stream content, string contentType, Dictionary<string, string>? metadata = null, CancellationToken ct = default)
        {
            await EnsureBucketExistsAsync(ct);

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(key)
                .WithStreamData(content)
                .WithObjectSize(content.Length)
                .WithContentType(contentType);

            if (metadata != null && metadata.Count > 0)
            {
                putObjectArgs = putObjectArgs.WithHeaders(new Dictionary<string, string>(
                    metadata.ToDictionary(
                        kvp => "X-Amz-Meta-" + kvp.Key,
                        kvp => Convert.ToBase64String(Encoding.UTF8.GetBytes(kvp.Value))
                    )
                ));
            }

            await client.PutObjectAsync(putObjectArgs, ct);
        }

        public async Task<Stream> GetAsync(string key, CancellationToken ct = default)
        {
            var memoryStream = new MemoryStream();

            var getObjectArgs = new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(key)
                .WithCallbackStream(stream =>
                {
                    stream.CopyTo(memoryStream);
                });

            await client.GetObjectAsync(getObjectArgs, ct);
            memoryStream.Position = 0;
            return memoryStream;
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        {
            try
            {
                var statObjectArgs = new StatObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(key);

                await client.StatObjectAsync(statObjectArgs, ct);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task DeleteAsync(string key, CancellationToken ct = default)
        {
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(key);

            await client.RemoveObjectAsync(removeObjectArgs, ct);
        }

        public async Task<ObjectMetadata?> StatAsync(string key, CancellationToken ct = default)
        {
            try
            {
                var statObjectArgs = new StatObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(key);

                var stat = await client.StatObjectAsync(statObjectArgs, ct);

                return new ObjectMetadata(
                    key,
                    stat.Size,
                    stat.ContentType,
                    stat.ETag
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<string> GetPresignedUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct = default)
        {
            var presignedGetObjectArgs = new PresignedGetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(key)
                .WithExpiry((int)expiresIn.TotalSeconds);

            return await client.PresignedGetObjectAsync(presignedGetObjectArgs);
        }

        /// <summary>
        /// 生成租户隔离的对象存储路径
        /// 按文件类型分目录：documents/ audios/ videos/ images/
        /// 格式: tenant-{tenantId}/{type}/{docId}.{ext}
        /// 例如:
        /// - tenant-019c18fe/documents/019c1923-20f4-7858-b5fb-0721220fb35b.pdf
        /// - tenant-019c18fe/audios/019c1923-20f4-7858-b5fb-0721220fb35b.mp3
        /// - tenant-019c18fe/videos/019c1923-20f4-7858-b5fb-0721220fb35b.mp4
        /// - tenant-019c18fe/images/019c1923-20f4-7858-b5fb-0721220fb35b.png
        /// </summary>
        public static string GenerateTenantObjectKey(string userId, string docId, string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var fileType = GetFileTypeDirectory(extension);
            return $"{userId}/{fileType}/{docId}{extension}";
        }

        /// <summary>
        /// 根据文件扩展名获取存储目录
        /// </summary>
        private static string GetFileTypeDirectory(string extension)
        {
            return extension switch
            {
                // 图片
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" or ".ico" => "images",

                // 音频
                ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".wma" or ".m4a" => "audios",

                // 视频
                ".mp4" or ".avi" or ".mov" or ".wmv" or ".flv" or ".mkv" or ".webm" or ".m4v" => "videos",

                // 文档（默认）
                _ => "documents"
            };
        }

        /// <summary>
        /// 从 ObjectKey 中提取文档ID
        /// </summary>
        public static string ExtractDocIdFromKey(string objectKey)
        {
            // 格式: tenant-{tenantId}/{type}/{docId}.{ext}
            // type 可以是: documents, audios, videos, images
            var parts = objectKey.Split('/');
            if (parts.Length >= 3)
            {
                var fileName = parts[2]; // {docId}.{ext}
                var docId = Path.GetFileNameWithoutExtension(fileName);
                return docId;
            }
            return objectKey;
        }

        /// <summary>
        /// 删除租户下的所有文档（租户级别清理）
        /// </summary>
        public async Task DeleteTenantAsync(string tenantId, CancellationToken ct = default)
        {
            var prefix = $"tenant-{tenantId}/";
            var listObjectsArgs = new ListObjectsArgs()
                .WithBucket(bucketName)
                .WithPrefix(prefix)
                .WithRecursive(true);

            var result = client.ListObjectsEnumAsync(listObjectsArgs, ct);

            var objectsToDelete = new List<string>();
            await foreach (var item in result)
            {
                objectsToDelete.Add(item.Key);
            }

            if (objectsToDelete.Count > 0)
            {
                foreach (var key in objectsToDelete)
                {
                    await DeleteAsync(key, ct);
                }
            }
        }

        /// <summary>
        /// 删除指定文档（基于 docId）
        /// </summary>
        public async Task DeleteDocumentAsync(string tenantId, string docId, CancellationToken ct = default)
        {
            // 新格式：tenant-{tenantId}/{type}/{docId}.{ext}
            // 由于不知道文件类型和扩展名，需要搜索所有类型目录
            var typeDirectories = new[] { "documents", "audios", "videos", "images" };
            var objectsToDelete = new List<string>();

            foreach (var typeDir in typeDirectories)
            {
                var prefix = $"tenant-{tenantId}/{typeDir}/{docId}";
                var listObjectsArgs = new ListObjectsArgs()
                    .WithBucket(bucketName)
                    .WithPrefix(prefix)
                    .WithRecursive(false); // 只查当前目录，不递归

                var result = client.ListObjectsEnumAsync(listObjectsArgs, ct);

                await foreach (var item in result)
                {
                    // 确保是 docId 开头的文件（避免匹配到 docId123 这样的文件）
                    if (Path.GetFileNameWithoutExtension(item.Key) == docId)
                    {
                        objectsToDelete.Add(item.Key);
                    }
                }
            }

            if (objectsToDelete.Count > 0)
            {
                foreach (var key in objectsToDelete)
                {
                    await DeleteAsync(key, ct);
                }
            }
        }

        /// <summary>
        /// 列出租户下的所有对象
        /// </summary>
        public async IAsyncEnumerable<string> ListTenantObjectsAsync(string tenantId)
        {
            var prefix = $"tenant-{tenantId}/";
            var listObjectsArgs = new ListObjectsArgs()
                .WithBucket(bucketName)
                .WithPrefix(prefix)
                .WithRecursive(true);

            var result = client.ListObjectsEnumAsync(listObjectsArgs);

            await foreach (var item in result)
            {
                yield return item.Key;
            }
        }
    }
}
