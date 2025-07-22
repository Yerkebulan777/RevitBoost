using Autodesk.Revit.DB;
using RevitUtils;
using System.Text;

namespace ExportPdfTool
{
    public class RevitPdfBatchExporter(Document document, string outputPath)
    {
        private readonly Document _document = document;
        private readonly string _outputPath = outputPath;

        public string ExportAllSheets(string exportFileName)
        {
            StringBuilder logBuilder = new();

            _ = logBuilder.AppendLine("=== PDF Export ===");
            _ = logBuilder.AppendLine($"Output path: {_outputPath}");
            _ = logBuilder.AppendLine($"Document: {_document.Title}");
            _ = logBuilder.AppendLine($"Export file: {exportFileName}");

            List<SheetModel> sheets = SheetModelUtility.GetSortedSheetModels(_document, true, out string output);

            _ = logBuilder.AppendLine($"Start PDF export for {sheets.Count} sheets...");
            _ = logBuilder.AppendLine($"Found {sheets.Count} valid sheets for export:");
            _ = logBuilder.AppendLine(output);

            if (sheets.Any())
            {
                try
                {
                    PDFExportOptions pdfOptions = CreatePDFOptions(exportFileName, ColorDepthType.Color);

                    if (_document.Export(_outputPath, [.. sheets.Select(s => s.SheetId)], pdfOptions))
                    {
                        _ = logBuilder.AppendLine($"✓ Successfully exported {sheets.Count} sheets");
                    }
                }
                catch (Exception ex)
                {
                    _ = logBuilder.Clear();
                    _ = logBuilder.AppendLine("=== PDF Export Operation Failed ===");
                    _ = logBuilder.AppendLine($"✗ Failed to export {sheets.Count} sheets");
                    _ = logBuilder.AppendLine($"✗ Error: {ex.Message}");
                }
            }

            return logBuilder.ToString();
        }


        private PDFExportOptions CreatePDFOptions(string fileName, ColorDepthType colorType)
        {
            return new PDFExportOptions
            {
                Combine = true,
                StopOnError = true,
                FileName = fileName,
                ColorDepth = colorType,
                PaperFormat = ExportPaperFormat.Default,
                RasterQuality = RasterQualityType.Medium,
                ExportQuality = PDFExportQualityType.DPI300,
                HideUnreferencedViewTags = true,
                HideCropBoundaries = true,
                HideReferencePlane = true,
                ZoomType = ZoomType.Zoom,
                HideScopeBoxes = true,
                ZoomPercentage = 100,
            };
        }



    }
}
