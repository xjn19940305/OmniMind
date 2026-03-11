using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using OmniMind.Ingestion;
using A = DocumentFormat.OpenXml.Drawing;
using Xunit;

namespace OmniMind.Ingestion.Tests;

public class FileParserTests
{
    [Fact]
    public async Task ParseAsync_Pptx_ReturnsSlideTitleAndBodyText()
    {
        var parser = new FileParser();
        await using var stream = CreatePresentation(
            ("第1页 标题", new[] { "第一段正文", "第二段正文" }));

        var text = await parser.ParseAsync(
            stream,
            "application/vnd.openxmlformats-officedocument.presentationml.presentation");

        Assert.Contains("第1页 标题", text);
        Assert.Contains("第一段正文", text);
        Assert.Contains("第二段正文", text);
    }

    [Fact]
    public async Task ParseAsync_Xlsx_ReturnsSheetNameAndCellText()
    {
        var parser = new FileParser();
        await using var stream = CreateWorkbook(
            "销售数据",
            new[]
            {
                new[] { "日期", "金额", "客户" },
                new[] { "2026-03-10", "1000", "A公司" }
            });

        var text = await parser.ParseAsync(
            stream,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        Assert.Contains("销售数据", text);
        Assert.Contains("日期 | 金额 | 客户", text);
        Assert.Contains("2026-03-10 | 1000 | A公司", text);
    }

    private static MemoryStream CreatePresentation((string title, string[] bodyLines) slideData)
    {
        var stream = new MemoryStream();
        using (var presentation = PresentationDocument.Create(stream, PresentationDocumentType.Presentation, true))
        {
            var presentationPart = presentation.AddPresentationPart();
            presentationPart.Presentation = new Presentation();

            var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>();
            slideMasterPart.SlideMaster = new SlideMaster(
                new CommonSlideData(new ShapeTree()),
                new SlideLayoutIdList(),
                new TextStyles());
            slideMasterPart.AddNewPart<ThemePart>().Theme = new A.Theme { Name = "Default" };

            var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>();
            slideLayoutPart.SlideLayout = new SlideLayout(new CommonSlideData(new ShapeTree()));
            slideMasterPart.SlideMaster.AppendChild(new SlideLayoutIdList(
                new SlideLayoutId { Id = 1U, RelationshipId = slideMasterPart.GetIdOfPart(slideLayoutPart) }));

            presentationPart.Presentation.SlideMasterIdList = new SlideMasterIdList(
                new SlideMasterId { Id = 2147483648U, RelationshipId = presentationPart.GetIdOfPart(slideMasterPart) });

            var slidePart = presentationPart.AddNewPart<SlidePart>();
            slidePart.Slide = new Slide(
                new CommonSlideData(
                    new ShapeTree(
                        new NonVisualGroupShapeProperties(
                            new NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                            new NonVisualGroupShapeDrawingProperties(),
                            new ApplicationNonVisualDrawingProperties()),
                        new GroupShapeProperties(new A.TransformGroup()),
                        CreateShape(2U, "Title", slideData.title),
                        CreateShape(3U, "Body", string.Join('\n', slideData.bodyLines)))),
                new ColorMapOverride(new A.MasterColorMapping()));

            presentationPart.Presentation.SlideIdList = new SlideIdList(
                new SlideId { Id = 256U, RelationshipId = presentationPart.GetIdOfPart(slidePart) });

            presentationPart.Presentation.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static Shape CreateShape(uint id, string name, string text)
    {
        var textBody = new TextBody(
            new A.BodyProperties(),
            new A.ListStyle());

        foreach (var line in text.Split('\n'))
        {
            textBody.AppendChild(new A.Paragraph(new A.Run(new A.Text(line))));
        }

        return new Shape(
            new NonVisualShapeProperties(
                new NonVisualDrawingProperties { Id = id, Name = name },
                new NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new ApplicationNonVisualDrawingProperties(new PlaceholderShape())),
            new ShapeProperties(),
            textBody);
    }

    private static MemoryStream CreateWorkbook(string sheetName, IReadOnlyList<string[]> rows)
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = new Row { RowIndex = (uint)(rowIndex + 1) };
                for (var columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
                {
                    row.AppendChild(new Cell
                    {
                        CellReference = $"{GetColumnName(columnIndex + 1)}{rowIndex + 1}",
                        DataType = CellValues.String,
                        CellValue = new CellValue(rows[rowIndex][columnIndex])
                    });
                }

                sheetData.AppendChild(row);
            }

            worksheetPart.Worksheet = new Worksheet(sheetData);

            workbookPart.Workbook.Sheets = new Sheets(
                new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1U,
                    Name = sheetName
                });
            workbookPart.Workbook.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static string GetColumnName(int index)
    {
        var dividend = index;
        var columnName = string.Empty;

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }
}
