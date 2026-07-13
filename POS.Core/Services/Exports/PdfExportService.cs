using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using POS.Core.Models.DTOs;

namespace POS.Core.Services.Exports
{
    public sealed class PdfExportService
    {
        private static readonly object FontLock = new();
        private static bool _fontConfigured;

        public void WriteTextPdf(
            string filePath,
            string title,
            string subtitle,
            string body,
            string generatedBy,
            string footerText = "")
        {
            if (string.IsNullOrWhiteSpace(body))
                throw new InvalidOperationException("There is no document content to export.");

            EnsureFontConfiguration();
            Document document = CreateBaseDocument(title, subtitle, generatedBy, footerText, landscape: false);
            Section section = document.LastSection;

            Paragraph paragraph = section.AddParagraph();
            paragraph.Format.Font.Name = "Courier New";
            paragraph.Format.Font.Size = 8.5;
            paragraph.Format.SpaceBefore = Unit.FromPoint(4);
            paragraph.AddText(NormalizeText(body));

            Render(document, filePath);
        }

        public void WriteTablePdf(string filePath, PdfTableDocumentDto data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.Columns.Count == 0)
                throw new InvalidOperationException("The PDF table has no columns.");
            if (data.Rows.Count == 0)
                throw new InvalidOperationException("There are no rows to export.");
            if (data.Rows.Any(row => row.Count != data.Columns.Count))
                throw new InvalidOperationException("A PDF table row does not match the column count.");

            EnsureFontConfiguration();
            bool landscape = data.Columns.Count > 6 || data.Columns.Sum(column => column.WidthCentimeters) > 17d;
            Document document = CreateBaseDocument(
                data.Title,
                data.Subtitle,
                data.GeneratedBy,
                data.FooterText,
                landscape);
            Section section = document.LastSection;

            if (!string.IsNullOrWhiteSpace(data.StoreHeading))
            {
                Paragraph store = section.AddParagraph(data.StoreHeading.Trim());
                store.Format.Font.Bold = true;
                store.Format.Font.Size = 9;
                store.Format.SpaceAfter = Unit.FromPoint(4);
            }

            foreach (string line in data.SummaryLines.Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                Paragraph summary = section.AddParagraph(line.Trim());
                summary.Format.Font.Size = 8.5;
                summary.Format.SpaceAfter = Unit.FromPoint(1);
            }

            if (data.SummaryLines.Count > 0)
                section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(2);

            Table table = section.AddTable();
            table.Borders.Width = Unit.FromPoint(0.4);
            table.Borders.Color = Colors.Gray;
            table.Rows.LeftIndent = Unit.Zero;

            foreach (PdfTableColumnDto column in data.Columns)
                table.AddColumn(Unit.FromCentimeter(Math.Max(1.2d, column.WidthCentimeters)));

            Row header = table.AddRow();
            header.HeadingFormat = true;
            header.Format.Font.Bold = true;
            header.Format.Font.Size = 7.5;
            header.Shading.Color = Colors.LightGray;

            for (int columnIndex = 0; columnIndex < data.Columns.Count; columnIndex++)
            {
                Paragraph cell = header.Cells[columnIndex].AddParagraph(data.Columns[columnIndex].Header);
                cell.Format.Alignment = data.Columns[columnIndex].IsNumeric
                    ? ParagraphAlignment.Right
                    : ParagraphAlignment.Left;
            }

            foreach (IReadOnlyList<string?> sourceRow in data.Rows)
            {
                Row row = table.AddRow();
                row.Format.Font.Size = 7.25;

                for (int columnIndex = 0; columnIndex < sourceRow.Count; columnIndex++)
                {
                    Paragraph cell = row.Cells[columnIndex].AddParagraph(NormalizeCell(sourceRow[columnIndex]));
                    cell.Format.Alignment = data.Columns[columnIndex].IsNumeric
                        ? ParagraphAlignment.Right
                        : ParagraphAlignment.Left;
                }
            }

            Render(document, filePath);
        }

        private static Document CreateBaseDocument(
            string title,
            string subtitle,
            string generatedBy,
            string footerText,
            bool landscape)
        {
            var document = new Document();
            document.Info.Title = title ?? string.Empty;
            document.Info.Author = generatedBy ?? string.Empty;

            Style? normal = document.Styles[StyleNames.Normal];
            if (normal == null)
                throw new InvalidOperationException("MigraDoc did not provide the default Normal style.");

            normal.Font.Name = "Arial";
            normal.Font.Size = 9;

            Section section = document.AddSection();
            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.Orientation = landscape ? Orientation.Landscape : Orientation.Portrait;
            section.PageSetup.TopMargin = Unit.FromCentimeter(1.2);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(1.3);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(1.2);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1.2);

            Paragraph heading = section.AddParagraph((title ?? string.Empty).Trim());
            heading.Format.Font.Size = 15;
            heading.Format.Font.Bold = true;
            heading.Format.Alignment = ParagraphAlignment.Center;
            heading.Format.SpaceAfter = Unit.FromPoint(2);

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                Paragraph subheading = section.AddParagraph(subtitle.Trim());
                subheading.Format.Font.Size = 9;
                subheading.Format.Alignment = ParagraphAlignment.Center;
                subheading.Format.SpaceAfter = Unit.FromPoint(5);
            }

            Paragraph generated = section.AddParagraph(
                $"Generated {DateTime.Now:yyyy-MM-dd HH:mm} by {NormalizeCell(generatedBy, "System")}");
            generated.Format.Font.Size = 7.5;
            generated.Format.Alignment = ParagraphAlignment.Right;
            generated.Format.SpaceAfter = Unit.FromPoint(6);

            Paragraph footer = section.Footers.Primary.AddParagraph();
            footer.Format.Font.Size = 7;
            footer.Format.Alignment = ParagraphAlignment.Center;
            if (!string.IsNullOrWhiteSpace(footerText))
            {
                footer.AddText(footerText.Trim());
                footer.AddLineBreak();
            }
            footer.AddText("Page ");
            footer.AddPageField();

            return document;
        }

        private static void Render(Document document, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("PDF file path is required.", nameof(filePath));

            string? folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            var renderer = new PdfDocumentRenderer
            {
                Document = document
            };
            renderer.RenderDocument();
            renderer.Save(filePath);

            var info = new FileInfo(filePath);
            if (!info.Exists || info.Length < 5)
                throw new IOException("The PDF file was not created correctly.");
        }

        private static void EnsureFontConfiguration()
        {
            if (_fontConfigured)
                return;

            lock (FontLock)
            {
                if (_fontConfigured)
                    return;

                if (OperatingSystem.IsWindows())
                    GlobalFontSettings.UseWindowsFontsUnderWindows = true;

                _fontConfigured = true;
            }
        }

        private static string NormalizeText(string value) =>
            (value ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');

        private static string NormalizeCell(string? value, string fallback = "")
        {
            string text = (value ?? string.Empty)
                .Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }
    }
}
