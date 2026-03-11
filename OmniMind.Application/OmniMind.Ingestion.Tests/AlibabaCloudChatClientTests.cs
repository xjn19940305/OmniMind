using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniMind.Ingestion;
using Xunit;

namespace OmniMind.Ingestion.Tests;

public class AlibabaCloudChatClientTests
{
    [Fact]
    public async Task CompleteStreamingAsync_IgnoresDoneSentinelAndYieldsDeltaChunks()
    {
        const string ssePayload =
            "data: {\"choices\":[{\"delta\":{\"content\":\"你\"}}]}\n\n" +
            "data: [DONE]\n\n";

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ssePayload, Encoding.UTF8)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return response;
        }))
        {
            BaseAddress = new Uri("https://example.com")
        };
        using var loggerFactory = LoggerFactory.Create(_ => { });
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var client = new AlibabaCloudChatClient(
            httpClient,
            new AlibabaCloudChatOptions { ApiKey = "test-key", Model = "qwen3.5-plus" },
            serviceProvider,
            loggerFactory.CreateLogger<AlibabaCloudChatClient>());

        var updates = new List<StreamingChatCompletionUpdate>();

        await foreach (var chunk in client.CompleteStreamingAsync(
            [new ChatMessage(ChatRole.User, "你好")]))
        {
            updates.Add(chunk);
        }

        var onlyUpdate = Assert.Single(updates);
        Assert.Equal("你", onlyUpdate.Text);
    }

    [Fact]
    public async Task CompleteStreamingAsync_Qwen35Plus_DisablesThinkingByDefault()
    {
        string? requestBody = null;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("data: [DONE]\n\n", Encoding.UTF8)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return response;
        }))
        {
            BaseAddress = new Uri("https://example.com")
        };
        using var loggerFactory = LoggerFactory.Create(_ => { });
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var client = new AlibabaCloudChatClient(
            httpClient,
            new AlibabaCloudChatOptions { ApiKey = "test-key", Model = "qwen3.5-plus" },
            serviceProvider,
            loggerFactory.CreateLogger<AlibabaCloudChatClient>());

        await foreach (var _ in client.CompleteStreamingAsync(
            [new ChatMessage(ChatRole.User, "你好")]))
        {
        }

        Assert.NotNull(requestBody);
        Assert.Contains("\"enable_thinking\":false", requestBody, StringComparison.Ordinal);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
