using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniMind.Ingestion;
using Xunit;

namespace OmniMind.Ingestion.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAlibabaCloudChatClient_UsesResolvedServiceProviderAndLogger()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AlibabaCloud:ApiKey"] = "test-key",
                ["AlibabaCloud:Chat:Endpoint"] = "https://dashscope.aliyuncs.com",
                ["AlibabaCloud:Chat:Model:0"] = "qwen3.5-plus"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<MarkerService>();
        services.AddLogging(builder => builder.AddProvider(new TestLoggerProvider()));
        services.AddAlibabaCloudChatClient(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var chatClient = Assert.IsType<AlibabaCloudChatClient>(
            serviceProvider.GetRequiredService<IChatClient>());

        var injectedServiceProvider = Assert.IsAssignableFrom<IServiceProvider>(
            GetPrivateField(chatClient, "serviceProvider"));
        var injectedLogger = Assert.IsAssignableFrom<ILogger<AlibabaCloudChatClient>>(
            GetPrivateField(chatClient, "logger"));
        var rootMarker = serviceProvider.GetRequiredService<MarkerService>();
        var injectedMarker = injectedServiceProvider.GetRequiredService<MarkerService>();

        Assert.Same(rootMarker, injectedMarker);
        Assert.DoesNotContain("NullLogger", injectedLogger.GetType().FullName);
    }

    private static object GetPrivateField(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field!.GetValue(instance)!;
    }

    private sealed class TestLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new TestLogger();

        public void Dispose()
        {
        }

        private sealed class TestLogger : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class MarkerService;
}
