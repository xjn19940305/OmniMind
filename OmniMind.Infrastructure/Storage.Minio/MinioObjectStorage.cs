using Minio;
using Minio.DataModel;
using Microsoft.Extensions.Options;
using OmniMind.Abstractions.Storage;
using OmniMind.Storage.Minio;
using Minio.DataModel.Args;

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

        public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            await EnsureBucketExistsAsync(ct);

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(key)
                .WithStreamData(content)
                .WithObjectSize(content.Length)
                .WithContentType(contentType);

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
        /// 格式: tenant-{tenantId}/documents/{docId}/{fileName}
        /// </summary>
        public static string GenerateTenantObjectKey(string tenantId, string docId, string fileName)
        {
            return $"tenant-{tenantId}/documents/{docId}/{fileName}";
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
        /// 删除指定文档的所有文件
        /// </summary>
        public async Task DeleteDocumentAsync(string tenantId, string docId, CancellationToken ct = default)
        {
            var prefix = $"tenant-{tenantId}/documents/{docId}/";
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
