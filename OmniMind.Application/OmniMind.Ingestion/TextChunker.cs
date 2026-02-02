using Microsoft.Extensions.Logging;
using OmniMind.Abstractions.Ingestion;
using System.Text;

namespace OmniMind.Ingestion
{
    /// <summary>
    /// 默认文本切片器实现
    /// </summary>
    public class TextChunker : IChunker
    {
        private readonly ILogger<TextChunker>? _logger;

        public TextChunker(ILogger<TextChunker>? logger = null)
        {
            _logger = logger;
        }

        public List<TextChunk> Chunk(string text, ChunkingOptions? options = null)
        {
            var opts = options ?? new ChunkingOptions();

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger?.LogWarning("[文本切片] 文本内容为空，跳过切片");
                return new List<TextChunk>();
            }

            var chunks = new List<TextChunk>();
            var paragraphs = SplitIntoParagraphs(text);

            if (paragraphs.Count == 0)
            {
                _logger?.LogWarning("[文本切片] 无法分割出段落");
                return new List<TextChunk>();
            }

            var currentChunk = new StringBuilder();
            var currentTokenCount = 0;
            var chunkIndex = 0;
            var overlapBuffer = new List<string>();

            foreach (var paragraph in paragraphs)
            {
                var paragraphTokenCount = EstimateTokenCount(paragraph);

                if (paragraphTokenCount > opts.MaxTokens)
                {
                    if (currentChunk.Length > 0)
                    {
                        chunks.Add(CreateChunk(currentChunk.ToString(), chunkIndex++, currentTokenCount));
                        UpdateOverlapBuffer(overlapBuffer, currentChunk.ToString(), opts.OverlapTokens);
                        currentChunk.Clear();
                        currentTokenCount = 0;
                    }

                    var longParagraphChunks = SplitLongParagraph(
                        paragraph,
                        opts.MaxTokens,
                        opts.OverlapTokens,
                        ref chunkIndex,
                        overlapBuffer);

                    chunks.AddRange(longParagraphChunks);
                    continue;
                }

                if (currentTokenCount + paragraphTokenCount > opts.MaxTokens && currentChunk.Length > 0)
                {
                    chunks.Add(CreateChunk(currentChunk.ToString(), chunkIndex++, currentTokenCount));
                    UpdateOverlapBuffer(overlapBuffer, currentChunk.ToString(), opts.OverlapTokens);

                    currentChunk.Clear();
                    currentTokenCount = 0;

                    foreach (var overlapText in overlapBuffer)
                    {
                        currentChunk.AppendLine(overlapText);
                        currentTokenCount += EstimateTokenCount(overlapText);
                    }
                }

                currentChunk.AppendLine(paragraph);
                currentTokenCount += paragraphTokenCount;
            }

            if (currentChunk.Length > 0)
            {
                chunks.Add(CreateChunk(currentChunk.ToString(), chunkIndex, currentTokenCount));
            }

            _logger?.LogInformation("[文本切片] 完成, 切片数量={ChunkCount}", chunks.Count);

            return chunks;
        }

        private static List<string> SplitIntoParagraphs(string text)
        {
            var paragraphs = new List<string>();
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var currentParagraph = new StringBuilder();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (string.IsNullOrEmpty(trimmedLine))
                {
                    if (currentParagraph.Length > 0)
                    {
                        paragraphs.Add(currentParagraph.ToString().Trim());
                        currentParagraph.Clear();
                    }
                }
                else
                {
                    if (IsNewParagraphStart(trimmedLine) && currentParagraph.Length > 0)
                    {
                        paragraphs.Add(currentParagraph.ToString().Trim());
                        currentParagraph.Clear();
                    }

                    currentParagraph.AppendLine(trimmedLine);
                }
            }

            if (currentParagraph.Length > 0)
            {
                paragraphs.Add(currentParagraph.ToString().Trim());
            }

            return paragraphs;
        }

        private static bool IsNewParagraphStart(string line)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^[一二三四五六七八九十]+、"))
                return true;

            if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d+[\.\、]"))
                return true;

            if (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("• "))
                return true;

            if (line.StartsWith("##") || line.StartsWith("###"))
                return true;

            return false;
        }

        private static List<TextChunk> SplitLongParagraph(
            string paragraph,
            int maxTokens,
            int overlapTokens,
            ref int chunkIndex,
            List<string> overlapBuffer)
        {
            var chunks = new List<TextChunk>();
            var sentences = SplitIntoSentences(paragraph);
            var currentChunk = new StringBuilder();
            var currentTokenCount = 0;

            foreach (var sentence in sentences)
            {
                var sentenceTokenCount = EstimateTokenCount(sentence);

                if (currentTokenCount + sentenceTokenCount > maxTokens && currentChunk.Length > 0)
                {
                    chunks.Add(CreateChunk(currentChunk.ToString(), chunkIndex++, currentTokenCount));
                    UpdateOverlapBuffer(overlapBuffer, currentChunk.ToString(), overlapTokens);

                    currentChunk.Clear();
                    currentTokenCount = 0;

                    foreach (var overlapText in overlapBuffer)
                    {
                        currentChunk.Append(overlapText).Append(" ");
                        currentTokenCount += EstimateTokenCount(overlapText);
                    }
                }

                currentChunk.Append(sentence).Append(" ");
                currentTokenCount += sentenceTokenCount;
            }

            if (currentChunk.Length > 0)
            {
                chunks.Add(CreateChunk(currentChunk.ToString(), chunkIndex++, currentTokenCount));
            }

            return chunks;
        }

        private static List<string> SplitIntoSentences(string text)
        {
            var sentences = new List<string>();
            var delimiters = new[] { '。', '！', '？', '.', '!', '?', ';', '；', '\n', '\r' };
            var currentSentence = new StringBuilder();

            foreach (var c in text)
            {
                currentSentence.Append(c);

                if (delimiters.Contains(c))
                {
                    var sentence = currentSentence.ToString().Trim();
                    if (!string.IsNullOrEmpty(sentence))
                    {
                        sentences.Add(sentence);
                    }
                    currentSentence.Clear();
                }
            }

            if (currentSentence.Length > 0)
            {
                sentences.Add(currentSentence.ToString().Trim());
            }

            return sentences;
        }

        private static void UpdateOverlapBuffer(List<string> overlapBuffer, string text, int overlapTokens)
        {
            overlapBuffer.Clear();
            var sentences = SplitIntoSentences(text);
            var currentTokenCount = 0;

            for (int i = sentences.Count - 1; i >= 0; i--)
            {
                var sentenceTokenCount = EstimateTokenCount(sentences[i]);

                if (currentTokenCount + sentenceTokenCount > overlapTokens)
                {
                    break;
                }

                overlapBuffer.Insert(0, sentences[i]);
                currentTokenCount += sentenceTokenCount;
            }
        }

        private static int EstimateTokenCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            var chineseChars = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
            var englishWords = System.Text.RegularExpressions.Regex.Matches(text, @"[a-zA-Z]+").Count;

            return chineseChars + englishWords;
        }

        private static TextChunk CreateChunk(string content, int index, int tokenCount)
        {
            return new TextChunk
            {
                Index = index,
                Content = content,
                TokenCount = tokenCount
            };
        }
    }
}
