using Autodesk.Revit.DB;
using RevitUtils;
using Serilog;
using System.Text;

namespace ExportPdfTool
{
    public class RevitPdfBatchExporter(Document document, string outputPath)
    {
        private readonly Document _document = document;
        private readonly string _outputPath = outputPath;

        public void ExportAllSheets(string exportFileName)
        {
            StringBuilder logBuilder = new();

            var sheets = SheetModelUtility.GetSheetModels(_document, true);

            _ = logBuilder.AppendLine("=== PDF Export ===");
            _ = logBuilder.AppendLine($"Output path: {_outputPath}");
            _ = logBuilder.AppendLine($"Document: {_document.Title}");
            _ = logBuilder.AppendLine($"Export file: {exportFileName}");
            _ = logBuilder.AppendLine($"Start PDF export for {sheets.Count} sheets...");
            _ = logBuilder.AppendLine($"Found {sheets.Count} valid sheets for export:");

            if (sheets.Any())
            {
                try
                {
                    PDFExportOptions pdfOptions = CreatePDFOptions(exportFileName, ColorDepthType.Color);

                    if (_document.Export(_outputPath, [.. sheets.Select(s => s.Id)], pdfOptions))
                    {
                        _ = logBuilder.AppendLine($"✓ Successfully exported {sheets.Count} sheets");
                        Log.Information(logBuilder.ToString());
                    }
                    else
                    {
                        _ = logBuilder.AppendLine($"⚠ Something wrong!");
                        Log.Warning(logBuilder.ToString());
                    }
                }
                catch (Exception ex)
                {
                    _ = logBuilder.Clear();
                    _ = logBuilder.AppendLine("=== PDF Export Operation Failed ===");
                    _ = logBuilder.AppendLine($"✗ Failed to export {sheets.Count} sheets");
                    _ = logBuilder.AppendLine($"✗ Error: {ex.Message}");
                    Log.Error(ex, logBuilder.ToString());
                }
            }
        }


        private PDFExportOptions CreatePDFOptions(string fileName, ColorDepthType colorType)
        {
            Log.Debug("Creating PDF export options");

            return new PDFExportOptions
            {
                Combine = true,
                StopOnError = true,
                FileName = fileName,
                HideScopeBoxes = true,
                ColorDepth = colorType,
                PaperFormat = ExportPaperFormat.Default,
                RasterQuality = RasterQualityType.Medium,
                ExportQuality = PDFExportQualityType.DPI300,
                HideUnreferencedViewTags = true,
                HideCropBoundaries = true,
                HideReferencePlane = true,
                ZoomType = ZoomType.Zoom,
                ZoomPercentage = 100,
            };
        }


    }
}
