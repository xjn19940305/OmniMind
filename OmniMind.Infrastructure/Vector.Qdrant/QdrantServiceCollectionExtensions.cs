using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using OmniMind.Abstractions.Storage;

namespace OmniMind.Vector.Qdrant
{
    public static class QdrantServiceCollectionExtensions
    {
        public static IServiceCollection AddQdrantService(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<QdrantOptions>(configuration.GetSection("QdrantOptions"));
            services.AddScoped<IVectorStore, QdrantVectorStore>();
            services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
                return new QdrantClient(options.Host ?? "localhost", options.Port, options.Https);
            });
            return services;
        }
    }
}
