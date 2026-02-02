using Microsoft.Extensions.Logging;
using OmniMind.Abstractions.Ingestion;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace OmniMind.Ingestion
{
    /// <summary>
    /// 默认文件解析器实现 - 跨平台版本
    /// 支持：PDF、DOCX、TXT、Markdown、音频转写、视频转写
    /// </summary>
    public class FileParser : IFileParser
    {
        private readonly ILogger<FileParser>? _logger;

        public FileParser(ILogger<FileParser>? logger = null)
        {
            _logger = logger;
        }

        private static readonly string[] SupportedContentTypes = new[]
        {
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "text/plain",
            "text/markdown",
            "audio/mp3", "audio/mpeg", "audio/wav", "audio/m4a", "audio/x-m4a",
            "video/mp4", "video/mpeg", "video/quicktime"
        };

        public Task<string> ParseAsync(
            Stream stream,
            string contentType,
            string? documentId = null,
            CancellationToken cancellationToken = default)
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            // 标准化 content type（处理 audio/mpeg vs audio/mp3 等）
            var normalizedContentType = contentType.ToLowerInvariant();

            return normalizedContentType switch
            {
                "application/pdf" => ParsePdfAsync(stream, cancellationToken),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                    => Task.Run(() => ParseDocx(stream), cancellationToken),
                "text/plain" or "text/markdown" => ParseTextAsync(stream, cancellationToken),

                // 音频/视频转写（统一使用 FunASR）
                var ct when ct.StartsWith("audio/") || ct.StartsWith("video/")
                    => ParseMediaAsync(stream, ct, cancellationToken),

                _ => throw new NotSupportedException($"不支持的文件类型: {contentType}")
            };
        }

        /// <summary>
        /// 音频/视频转写（使用 FunASR）
        /// </summary>
        private async Task<string> ParseMediaAsync(Stream stream, string contentType, CancellationToken ct)
        {
            try
            {
                _logger?.LogInformation("开始媒体转写: ContentType={ContentType}", contentType);

                // TODO: 在这里实现 FunASR 转写逻辑
                // 1. 如果是视频，先提取音频（可以使用 FFmpeg）
                // 2. 调用 FunASR 进行语音转写
                // 3. 返回转写文本

                // 示例代码（需要实现）：
                // if (contentType.StartsWith("video/"))
                // {
                //     stream = await ExtractAudioAsync(stream);
                // }
                // var transcription = await funAsrService.TranscribeAsync(stream);
                // return transcription.Text;

                throw new NotImplementedException("FunASR 转写功能待实现，请在这里集成 FunASR 服务");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "媒体转写失败: ContentType={ContentType}", contentType);
                throw new InvalidOperationException("媒体转写失败", ex);
            }
        }

        public bool IsSupported(string contentType)
        {
            return SupportedContentTypes.Contains(contentType?.ToLowerInvariant());
        }

        private async Task<string> ParsePdfAsync(Stream stream, CancellationToken ct)
        {
            try
            {
                var text = new StringBuilder();

                using var pdfDocument = PdfDocument.Open(stream);
                foreach (var page in pdfDocument.GetPages())
                {
                    ct.ThrowIfCancellationRequested();
                    text.AppendLine(page.Text);
                }

                return await Task.FromResult(PostProcessText(text.ToString()));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "PDF 解析失败");
                throw new InvalidOperationException("PDF 文件解析失败", ex);
            }
        }

        private string ParseDocx(Stream stream)
        {
            try
            {
                using var wordDocument = WordprocessingDocument.Open(stream, false);
                var mainPart = wordDocument.MainDocumentPart;
                if (mainPart?.Document?.Body == null)
                {
                    return string.Empty;
                }

                var body = mainPart.Document.Body;
                var text = new StringBuilder();

                foreach (var paragraph in body.Elements<Paragraph>())
                {
                    var paragraphText = paragraph.InnerText;
                    if (!string.IsNullOrWhiteSpace(paragraphText))
                    {
                        text.AppendLine(paragraphText);
                    }
                }

                foreach (var table in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>())
                {
                    foreach (var row in table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>())
                    {
                        var rowText = new List<string>();
                        foreach (var cell in row.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>())
                        {
                            var cellText = string.Join(" ", cell.Elements<Paragraph>().Select(p => p.InnerText));
                            rowText.Add(cellText.Trim());
                        }
                        text.AppendLine(string.Join(" | ", rowText));
                    }
                    text.AppendLine();
                }

                return PostProcessText(text.ToString());
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "DOCX 解析失败");
                throw new InvalidOperationException("DOCX 文件解析失败", ex);
            }
        }

        private async Task<string> ParseTextAsync(Stream stream, CancellationToken ct)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var text = await reader.ReadToEndAsync(ct);
            return PostProcessText(text);
        }

        private static string PostProcessText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new StringBuilder();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (!string.IsNullOrEmpty(trimmedLine))
                {
                    result.AppendLine(trimmedLine);
                }
            }

            return result.ToString().TrimEnd();
        }
    }
}
