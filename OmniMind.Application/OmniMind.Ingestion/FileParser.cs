using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using OmniMind.Abstractions.Ingestion;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace OmniMind.Ingestion
{
    public class FileParser : IFileParser
    {
        private static readonly string[] SupportedContentTypes =
        {
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
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

        private static readonly Regex PageNumberRegex = new(
            @"^(第?\s*\d+\s*页(\s*/\s*共?\s*\d+\s*页)?|\d+\s*/\s*\d+|page\s*\d+(\s*of\s*\d+)?)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] WatermarkKeywords =
        {
            "confidential",
            "draft",
            "仅供内部使用",
            "内部资料",
            "机密",
            "保密"
        };

        private readonly ILogger<FileParser>? logger;

        public FileParser(ILogger<FileParser>? logger = null)
        {
            this.logger = logger;
        }

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
                "application/vnd.openxmlformats-officedocument.presentationml.presentation" => Task.Run(() => ParsePptx(stream), cancellationToken),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => Task.Run(() => ParseXlsx(stream), cancellationToken),
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
                using var pdfDocument = PdfDocument.Open(stream);
                var pageBlocks = new List<StructuredBlock>();

                foreach (var page in pdfDocument.GetPages())
                {
                    ct.ThrowIfCancellationRequested();
                    pageBlocks.Add(new StructuredBlock(
                        $"[[PAGE:{page.Number}]]",
                        SplitNormalizedLines(page.Text)));
                }

                return PostProcessStructuredBlocks(pageBlocks);
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

                var lines = new List<string>();
                foreach (var child in mainPart.Document.Body.ChildElements)
                {
                    switch (child)
                    {
                        case Paragraph paragraph:
                            AppendParagraph(lines, paragraph);
                            break;
                        case W.Table table:
                            lines.AddRange(ExtractWordTableLines(table));
                            break;
                    }
                }

                return PostProcessText(string.Join(Environment.NewLine, lines));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "DOCX parse failed");
                throw new InvalidOperationException("DOCX parse failed", ex);
            }
        }

        private string ParsePptx(Stream stream)
        {
            try
            {
                using var presentation = PresentationDocument.Open(stream, false);
                var presentationPart = presentation.PresentationPart;
                if (presentationPart?.Presentation?.SlideIdList == null)
                {
                    return string.Empty;
                }

                var blocks = new List<StructuredBlock>();
                var slideIndex = 0;

                foreach (var slideId in presentationPart.Presentation.SlideIdList.Elements<SlideId>())
                {
                    slideIndex++;
                    if (presentationPart.GetPartById(slideId.RelationshipId!) is not SlidePart slidePart)
                    {
                        continue;
                    }

                    var texts = slidePart.Slide
                        .Descendants<A.Text>()
                        .Select(t => NormalizeLine(t.Text))
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .ToList();

                    if (texts.Count == 0)
                    {
                        continue;
                    }

                    var lines = new List<string> { $"# {texts[0]}" };
                    foreach (var text in texts.Skip(1))
                    {
                        lines.Add(text);
                    }

                    blocks.Add(new StructuredBlock($"[[SLIDE:{slideIndex}]]", lines));
                }

                return PostProcessStructuredBlocks(blocks);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "PPTX parse failed");
                throw new InvalidOperationException("PPTX parse failed", ex);
            }
        }

        private string ParseXlsx(Stream stream)
        {
            try
            {
                using var workbook = SpreadsheetDocument.Open(stream, false);
                var workbookPart = workbook.WorkbookPart;
                if (workbookPart?.Workbook?.Sheets == null)
                {
                    return string.Empty;
                }

                var blocks = new List<StructuredBlock>();
                foreach (var sheet in workbookPart.Workbook.Sheets.Elements<Sheet>())
                {
                    if (sheet.Id?.Value == null)
                    {
                        continue;
                    }

                    if (workbookPart.GetPartById(sheet.Id.Value) is not WorksheetPart worksheetPart)
                    {
                        continue;
                    }

                    var lines = new List<string> { $"# Sheet: {sheet.Name}" };
                    var rows = worksheetPart.Worksheet.Descendants<Row>();

                    foreach (var row in rows)
                    {
                        var values = row.Elements<Cell>()
                            .Select(cell => ReadCellText(workbookPart, cell))
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                            .ToList();

                        if (values.Count > 0)
                        {
                            lines.Add(string.Join(" | ", values));
                        }
                    }

                    blocks.Add(new StructuredBlock($"[[SHEET:{sheet.Name}]]", lines));
                }

                return PostProcessStructuredBlocks(blocks);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "XLSX parse failed");
                throw new InvalidOperationException("XLSX parse failed", ex);
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

        private static void AppendParagraph(List<string> lines, Paragraph paragraph)
        {
            var text = NormalizeLine(paragraph.InnerText);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var headingLevel = TryGetHeadingLevel(paragraph, text);
            if (headingLevel > 0)
            {
                lines.Add($"{new string('#', headingLevel)} {text}");
                return;
            }

            lines.Add(text);
        }

        private static int TryGetHeadingLevel(Paragraph paragraph, string text)
        {
            var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            if (!string.IsNullOrWhiteSpace(styleId))
            {
                var digits = new string(styleId.Where(char.IsDigit).ToArray());
                if (styleId.Contains("Heading", StringComparison.OrdinalIgnoreCase) && int.TryParse(digits, out var headingLevel))
                {
                    return Math.Clamp(headingLevel, 1, 6);
                }

                if (styleId.Contains("标题", StringComparison.OrdinalIgnoreCase) && int.TryParse(digits, out headingLevel))
                {
                    return Math.Clamp(headingLevel, 1, 6);
                }
            }

            if (Regex.IsMatch(text, @"^(第[\d一二三四五六七八九十百]+[章节]|[一二三四五六七八九十]+、|\d+(\.\d+){0,2})"))
            {
                return text.Count(c => c == '.') switch
                {
                    0 => 1,
                    1 => 2,
                    _ => 3
                };
            }

            return 0;
        }

        private static IEnumerable<string> ExtractWordTableLines(W.Table table)
        {
            foreach (var row in table.Elements<W.TableRow>())
            {
                var cells = row.Elements<W.TableCell>()
                    .Select(cell => NormalizeLine(string.Join(" ", cell.Elements<Paragraph>().Select(p => p.InnerText))))
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToList();

                if (cells.Count > 0)
                {
                    yield return string.Join(" | ", cells);
                }
            }
        }

        private static string ReadCellText(WorkbookPart workbookPart, Cell cell)
        {
            if (cell.CellValue == null && cell.InlineString == null)
            {
                return string.Empty;
            }

            var rawValue = cell.CellValue?.Text ?? cell.InnerText;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return string.Empty;
            }

            if (cell.DataType?.Value == CellValues.SharedString &&
                int.TryParse(rawValue, out var sharedStringIndex) &&
                workbookPart.SharedStringTablePart?.SharedStringTable != null)
            {
                var item = workbookPart.SharedStringTablePart.SharedStringTable.Elements<SharedStringItem>()
                    .ElementAtOrDefault(sharedStringIndex);
                return NormalizeLine(item?.InnerText ?? string.Empty);
            }

            if (cell.DataType?.Value == CellValues.InlineString)
            {
                return NormalizeLine(cell.InlineString?.InnerText ?? string.Empty);
            }

            return NormalizeLine(rawValue);
        }

        private static List<string> SplitNormalizedLines(string text)
        {
            return text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeLine)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
        }

        private static string PostProcessStructuredBlocks(IReadOnlyList<StructuredBlock> blocks)
        {
            if (blocks.Count == 0)
            {
                return string.Empty;
            }

            var repeatedNoiseLines = FindRepeatedNoiseLines(blocks);
            var builder = new StringBuilder();

            foreach (var block in blocks)
            {
                builder.AppendLine(block.Marker);
                var previousLine = string.Empty;

                foreach (var line in block.Lines)
                {
                    if (repeatedNoiseLines.Contains(line) || ShouldDropLine(line))
                    {
                        continue;
                    }

                    if (string.Equals(previousLine, line, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    builder.AppendLine(line);
                    previousLine = line;
                }

                builder.AppendLine();
            }

            return PostProcessText(builder.ToString());
        }

        private static HashSet<string> FindRepeatedNoiseLines(IReadOnlyList<StructuredBlock> blocks)
        {
            if (blocks.Count < 2)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var threshold = Math.Max(2, (int)Math.Ceiling(blocks.Count * 0.6));
            return blocks
                .SelectMany(block => block.Lines.Distinct(StringComparer.Ordinal))
                .Where(line => line.Length <= 40)
                .GroupBy(line => line, StringComparer.Ordinal)
                .Where(group => group.Count() >= threshold)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.Ordinal);
        }

        private static bool ShouldDropLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return true;
            }

            if (PageNumberRegex.IsMatch(line))
            {
                return true;
            }

            var lower = line.ToLowerInvariant();
            return WatermarkKeywords.Any(keyword => lower.Contains(keyword));
        }

        private static string PostProcessText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var previousLineWasBlank = false;

            foreach (var rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.None))
            {
                var line = NormalizeLine(rawLine);
                if (line.StartsWith("[[", StringComparison.Ordinal) && line.EndsWith("]]", StringComparison.Ordinal))
                {
                    if (builder.Length > 0 && !previousLineWasBlank)
                    {
                        builder.AppendLine();
                    }

                    builder.AppendLine(line);
                    previousLineWasBlank = false;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    if (!previousLineWasBlank && builder.Length > 0)
                    {
                        builder.AppendLine();
                    }

                    previousLineWasBlank = true;
                    continue;
                }

                if (ShouldDropLine(line))
                {
                    continue;
                }

                builder.AppendLine(line);
                previousLineWasBlank = false;
            }

            return builder.ToString().Trim();
        }

        private static string NormalizeLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            var normalized = line
                .Replace('\u3000', ' ')
                .Replace('\t', ' ')
                .Replace('：', ':')
                .Replace('（', '(')
                .Replace('）', ')');

            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
            return normalized;
        }

        private sealed record StructuredBlock(string Marker, List<string> Lines);
    }
}
