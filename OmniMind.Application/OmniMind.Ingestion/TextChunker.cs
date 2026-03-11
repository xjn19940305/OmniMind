using Microsoft.Extensions.Logging;
using OmniMind.Abstractions.Ingestion;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OmniMind.Ingestion
{
    /// <summary>
    /// 结构化文本切片器：优先按标题、段落、表格、代码块切分，再对过长 section 做 token 切分。
    /// </summary>
    public class TextChunker : IChunker
    {
        private readonly ILogger<TextChunker>? logger;

        public TextChunker(ILogger<TextChunker>? logger = null)
        {
            this.logger = logger;
        }

        public List<TextChunk> Chunk(string text, ChunkingOptions? options = null)
        {
            var opts = options ?? new ChunkingOptions
            {
                MaxTokens = 700,
                OverlapTokens = 100
            };

            if (string.IsNullOrWhiteSpace(text))
            {
                logger?.LogWarning("[文本切片] 文本为空，跳过切片");
                return new List<TextChunk>();
            }

            var documentTitle = opts.DocumentTitle;
            var sections = ExtractSections(text, ref documentTitle);
            if (sections.Count == 0)
            {
                logger?.LogWarning("[文本切片] 未识别到可切片 section");
                return new List<TextChunk>();
            }

            var chunks = new List<TextChunk>();
            var seenHashes = new HashSet<string>(StringComparer.Ordinal);
            var chunkIndex = 0;

            foreach (var section in sections)
            {
                var prefix = BuildPrefix(documentTitle, opts, section);
                var prefixTokens = EstimateTokenCount(prefix);
                var availableTokens = Math.Max(opts.MinTokens, opts.MaxTokens - prefixTokens);

                foreach (var body in SplitSectionBody(section.Content, availableTokens, opts.OverlapTokens))
                {
                    var content = BuildChunkContent(prefix, body);
                    var contentHash = ComputeHash(content);
                    if (!seenHashes.Add(contentHash))
                    {
                        continue;
                    }

                    var metadata = new Dictionary<string, string>(section.Metadata, StringComparer.Ordinal)
                    {
                        ["content_hash"] = contentHash
                    };

                    if (!string.IsNullOrWhiteSpace(documentTitle))
                    {
                        metadata["document_title"] = documentTitle;
                    }

                    if (!string.IsNullOrWhiteSpace(section.SectionPath))
                    {
                        metadata["section_path"] = section.SectionPath;
                    }

                    if (!string.IsNullOrWhiteSpace(opts.SourceType))
                    {
                        metadata["source_type"] = opts.SourceType;
                    }

                    if (!string.IsNullOrWhiteSpace(opts.SourceName))
                    {
                        metadata["source_name"] = opts.SourceName;
                    }

                    chunks.Add(new TextChunk
                    {
                        Index = chunkIndex++,
                        Content = content,
                        TokenCount = EstimateTokenCount(content),
                        Metadata = metadata
                    });
                }
            }

            logger?.LogInformation("[文本切片] 完成，切片数量={ChunkCount}", chunks.Count);
            return chunks;
        }

        private static List<Section> ExtractSections(string text, ref string? documentTitle)
        {
            var sections = new List<Section>();
            var headingStack = new List<string>();
            var currentLines = new List<string>();
            var currentMetadata = new Dictionary<string, string>(StringComparer.Ordinal);
            var inCodeBlock = false;

            void FlushCurrent()
            {
                var content = string.Join(Environment.NewLine, currentLines).Trim();
                currentLines.Clear();

                if (string.IsNullOrWhiteSpace(content))
                {
                    return;
                }

                sections.Add(new Section(
                    content,
                    headingStack.Count == 0 ? string.Empty : string.Join(" > ", headingStack),
                    new Dictionary<string, string>(currentMetadata, StringComparer.Ordinal)));
            }

            foreach (var rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.None))
            {
                var line = NormalizeLine(rawLine);

                if (TryParseMarker(line, currentMetadata))
                {
                    FlushCurrent();
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    if (inCodeBlock)
                    {
                        currentLines.Add(string.Empty);
                    }
                    else
                    {
                        FlushCurrent();
                    }

                    continue;
                }

                if (line.StartsWith("文档标题:", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(documentTitle))
                {
                    documentTitle = line["文档标题:".Length..].Trim();
                    continue;
                }

                if (line == "```")
                {
                    inCodeBlock = !inCodeBlock;
                    currentLines.Add(line);
                    continue;
                }

                if (inCodeBlock)
                {
                    currentLines.Add(line);
                    continue;
                }

                if (TryParseHeading(line, out var level, out var headingText))
                {
                    FlushCurrent();
                    UpdateHeadingStack(headingStack, level, headingText);
                    continue;
                }

                currentLines.Add(line);
            }

            FlushCurrent();
            return sections;
        }

        private static bool TryParseMarker(string line, Dictionary<string, string> metadata)
        {
            if (!line.StartsWith("[[", StringComparison.Ordinal) || !line.EndsWith("]]", StringComparison.Ordinal))
            {
                return false;
            }

            var markerContent = line[2..^2];
            var parts = markerContent.Split(':', 2);
            if (parts.Length != 2)
            {
                return false;
            }

            metadata.Clear();
            switch (parts[0].ToUpperInvariant())
            {
                case "PAGE":
                    metadata["page_no"] = parts[1];
                    break;
                case "SLIDE":
                    metadata["slide_no"] = parts[1];
                    break;
                case "SHEET":
                    metadata["sheet_name"] = parts[1];
                    break;
                default:
                    metadata["source_ref"] = parts[1];
                    break;
            }

            return true;
        }

        private static bool TryParseHeading(string line, out int level, out string headingText)
        {
            var markdown = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
            if (markdown.Success)
            {
                level = markdown.Groups[1].Value.Length;
                headingText = markdown.Groups[2].Value.Trim();
                return true;
            }

            if (Regex.IsMatch(line, @"^(第[\d一二三四五六七八九十百]+[章节]|[一二三四五六七八九十]+、|\d+(\.\d+){0,2})"))
            {
                level = line.Count(c => c == '.') switch
                {
                    0 => 1,
                    1 => 2,
                    _ => 3
                };
                headingText = line.Trim();
                return true;
            }

            level = 0;
            headingText = string.Empty;
            return false;
        }

        private static void UpdateHeadingStack(List<string> stack, int level, string headingText)
        {
            while (stack.Count >= level)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            stack.Add(headingText);
        }

        private static string BuildPrefix(string? documentTitle, ChunkingOptions options, Section section)
        {
            var prefix = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(documentTitle))
            {
                prefix.AppendLine($"文档标题:{documentTitle}");
            }

            if (!string.IsNullOrWhiteSpace(section.SectionPath))
            {
                prefix.AppendLine($"章节路径:{section.SectionPath}");
            }

            if (!string.IsNullOrWhiteSpace(options.SourceType))
            {
                prefix.AppendLine($"来源类型:{options.SourceType}");
            }

            if (!string.IsNullOrWhiteSpace(options.SourceName))
            {
                prefix.AppendLine($"来源名称:{options.SourceName}");
            }

            if (section.Metadata.TryGetValue("page_no", out var pageNo))
            {
                prefix.AppendLine($"页码:{pageNo}");
            }

            if (section.Metadata.TryGetValue("slide_no", out var slideNo))
            {
                prefix.AppendLine($"幻灯片:{slideNo}");
            }

            if (section.Metadata.TryGetValue("sheet_name", out var sheetName))
            {
                prefix.AppendLine($"工作表:{sheetName}");
            }

            return prefix.ToString().TrimEnd();
        }

        private static IEnumerable<string> SplitSectionBody(string content, int maxTokens, int overlapTokens)
        {
            if (EstimateTokenCount(content) <= maxTokens)
            {
                yield return content.Trim();
                yield break;
            }

            var segments = SplitIntoSegments(content);
            var current = new List<string>();
            var currentTokens = 0;

            foreach (var segment in segments)
            {
                var segmentTokens = EstimateTokenCount(segment);
                if (currentTokens + segmentTokens > maxTokens && current.Count > 0)
                {
                    yield return string.Join(Environment.NewLine, current).Trim();

                    var overlap = BuildOverlap(current, overlapTokens);
                    current = overlap.ToList();
                    currentTokens = current.Sum(EstimateTokenCount);
                }

                current.Add(segment);
                currentTokens += segmentTokens;
            }

            if (current.Count > 0)
            {
                yield return string.Join(Environment.NewLine, current).Trim();
            }
        }

        private static List<string> SplitIntoSegments(string content)
        {
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeLine)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            if (lines.Count > 1)
            {
                return lines;
            }

            var parts = Regex.Split(content, @"(?<=[。！？!?；;])")
                .Select(NormalizeLine)
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToList();

            return parts.Count > 0 ? parts : new List<string> { NormalizeLine(content) };
        }

        private static IEnumerable<string> BuildOverlap(IReadOnlyList<string> segments, int overlapTokens)
        {
            var selected = new List<string>();
            var tokens = 0;

            for (var index = segments.Count - 1; index >= 0; index--)
            {
                var segmentTokens = EstimateTokenCount(segments[index]);
                if (tokens + segmentTokens > overlapTokens)
                {
                    break;
                }

                selected.Insert(0, segments[index]);
                tokens += segmentTokens;
            }

            return selected;
        }

        private static string BuildChunkContent(string prefix, string body)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return body.Trim();
            }

            return $"{prefix}{Environment.NewLine}{Environment.NewLine}正文:{Environment.NewLine}{body.Trim()}";
        }

        private static string NormalizeLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            return Regex.Replace(
                line.Replace('\u3000', ' ').Replace('\t', ' ').Replace('：', ':'),
                @"\s+",
                " ").Trim();
        }

        private static int EstimateTokenCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            var chineseChars = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
            var englishWords = Regex.Matches(text, @"[a-zA-Z0-9_/\-]+").Count;
            return chineseChars + englishWords;
        }

        private static string ComputeHash(string content)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
            return Convert.ToHexString(bytes);
        }

        private sealed record Section(string Content, string SectionPath, Dictionary<string, string> Metadata);
    }
}
