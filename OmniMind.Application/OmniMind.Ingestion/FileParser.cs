using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using OmniMind.Abstractions.Ingestion;
using System.Text;
using UglyToad.PdfPig;

namespace OmniMind.Ingestion
{
    public class FileParser : IFileParser
    {
        private readonly ILogger<FileParser>? logger;

        public FileParser(ILogger<FileParser>? logger = null)
        {
            this.logger = logger;
        }

        private static readonly string[] SupportedContentTypes =
        {
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "text/plain",
            "text/markdown",
            "image/jpeg",
            "image/jpg",
            "image/png",
            "image/gif",
            "image/bmp",
            "image/webp",
            "audio/mp3",
            "audio/mpeg",
            "audio/wav",
            "audio/m4a",
            "audio/x-m4a",
            "video/mp4",
            "video/mpeg",
            "video/quicktime"
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

            var normalizedContentType = contentType.ToLowerInvariant();

            return normalizedContentType switch
            {
                "application/pdf" => ParsePdfAsync(stream, cancellationToken),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => Task.Run(() => ParseDocx(stream), cancellationToken),
                "text/plain" or "text/markdown" => ParseTextAsync(stream, cancellationToken),
                var ct when ct.StartsWith("image/") => ParseImageAsync(stream, ct, cancellationToken),
                var ct when ct.StartsWith("audio/") || ct.StartsWith("video/") => ParseMediaAsync(stream, ct, cancellationToken),
                _ => throw new NotSupportedException($"Unsupported content type: {contentType}")
            };
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

                return PostProcessText(text.ToString());
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "PDF parse failed");
                throw new InvalidOperationException("PDF parse failed", ex);
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
                logger?.LogError(ex, "DOCX parse failed");
                throw new InvalidOperationException("DOCX parse failed", ex);
            }
        }

        private async Task<string> ParseTextAsync(Stream stream, CancellationToken ct)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return PostProcessText(await reader.ReadToEndAsync(ct));
        }

        private async Task<string> ParseImageAsync(Stream stream, string contentType, CancellationToken ct)
        {
            try
            {
                logger?.LogInformation(
                    "Image OCR is not configured yet. Writing placeholder text. ContentType={ContentType}",
                    contentType);
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, ct);
                return $"[image]\ncontent_type: {contentType}\nbyte_length: {buffer.Length}\nocr_status: pending_external_ocr";
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Image parse failed: ContentType={ContentType}", contentType);
                throw new InvalidOperationException("Image parse failed", ex);
            }
        }

        private async Task<string> ParseMediaAsync(Stream stream, string contentType, CancellationToken ct)
        {
            try
            {
                logger?.LogWarning(
                    "Media parsing was invoked directly. Media files must go through the async transcription queue first. ContentType={ContentType}",
                    contentType);

                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, ct);

                throw new InvalidOperationException(
                    $"Media files must be transcribed asynchronously before parsing. ContentType={contentType}, ByteLength={buffer.Length}");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Media parse failed: ContentType={ContentType}", contentType);
                throw;
            }
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
