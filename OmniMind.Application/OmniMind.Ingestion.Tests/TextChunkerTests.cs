using OmniMind.Abstractions.Ingestion;
using OmniMind.Ingestion;
using Xunit;

namespace OmniMind.Ingestion.Tests;

public class TextChunkerTests
{
    [Fact]
    public void Chunk_StructuredDocument_KeepsHeadingContextInChunk()
    {
        var chunker = new TextChunker();
        const string text = """
            文档标题：订单管理规范

            # 第二章 订单创建

            ## 创建前校验

            创建订单前必须校验库存、价格、活动状态。
            创建订单前必须校验库存、价格、活动状态。
            创建订单前必须校验库存、价格、活动状态。

            ## 创建成功后处理

            订单创建成功后，需要生成订单号并发送通知。
            """;

        var chunks = chunker.Chunk(text, new ChunkingOptions
        {
            MaxTokens = 30,
            OverlapTokens = 5
        });

        Assert.True(chunks.Count >= 2);
        Assert.All(chunks, chunk => Assert.Contains("文档标题:订单管理规范", chunk.Content));
    }
}
