using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using OmniMind.Abstractions.Storage;

namespace OmniMind.Storage.Minio
{
    public static class MinioServiceCollectionExtensions
    {
        public static IServiceCollection AddMinioService(this IServiceCollection services, IConfiguration configuration)
        {

            services.Configure<OssOptions>(configuration.GetSection("OssOptions"));
            services.AddScoped<IObjectStorage, MinioObjectStorage>();
            services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<OssOptions>>().Value;
                return new MinioClient()
                                   .WithEndpoint(options.Endpoint)
                                   .WithCredentials(options.AccessKey, options.SecretKey)
                                   //.WithSSL()
                                   .Build();
            });
            return services;
        }
    }
}
