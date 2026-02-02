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
            services.AddHttpClient<QdrantHttpVectorStore>();
            services.AddScoped<IVectorStore, QdrantHttpVectorStore>();
            return services;
        }
    }
}
