using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OmniMind.Abstractions.Storage;

namespace OmniMind.Vector.Qdrant
{
    public static class QdrantServiceCollectionExtensions
    {
        public static IServiceCollection AddQdrantService(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<QdrantOptions>(configuration.GetSection("QdrantOptions"));

            // 使用命名 HttpClient 而不是类型化 HttpClient
            services.AddHttpClient("Qdrant");

            services.AddScoped<IVectorStore, QdrantHttpVectorStore>();
            return services;
        }
    }
}
