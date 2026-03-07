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
        /// 生成对象存储路径，按用户和文件类型分目录。
        /// </summary>
        public static string GenerateObjectKey(string userId, string docId, string fileName)
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
            var parts = objectKey.Split('/');
            if (parts.Length >= 3)
            {
                var fileName = parts[2]; // {docId}.{ext}
                var docId = Path.GetFileNameWithoutExtension(fileName);
                return docId;
            }
            return objectKey;
        }
    }
}
