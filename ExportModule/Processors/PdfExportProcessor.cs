using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ExportModule.Core;
using RevitUtils;
using System.Diagnostics;

namespace ExportModule.Processors
{
    /// <summary>
    /// Процессор экспорта в PDF
    /// </summary>
    public class PdfExportProcessor : IExportProcessor
    {
        public ExportType Type => ExportType.Pdf;
        public string FileExtension => ".pdf";
        public string FolderName => "03_PDF";

        public ExportResult Execute(UIDocument uidoc, ExportRequest request)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                string exportDirectory = Path.Combine(request.BaseExportDirectory, FolderName);
                _ = Directory.CreateDirectory(exportDirectory);

                string outputFilePath = Path.Combine(exportDirectory, $"{request.RevitFileName}{FileExtension}");

                // Получаем листы для экспорта
                List<SheetModel> sheets = SheetModelUtility.GetSortedSheetModels(uidoc.Document, true, out string sheetLog);

                if (!sheets.Any())
                {
                    return ExportResult.Failure("No valid sheets found for PDF export");
                }

                // Создаем PDF опции
                PDFExportOptions pdfOptions = CreatePDFOptions(request.RevitFileName,
                    request.UseColorForPdf ? ColorDepthType.Color : ColorDepthType.GrayScale);

                // Выполняем экспорт
                List<ElementId> sheetIds = [.. sheets.Select(s => s.SheetId)];
                bool success = uidoc.Document.Export(exportDirectory, sheetIds, pdfOptions);

                if (success)
                {
                    stopwatch.Stop();

                    if (request.OpenFolderAfterExport && File.Exists(outputFilePath))
                    {
                        OpenFolder(exportDirectory);
                    }

                    return ExportResult.Success(outputFilePath, stopwatch.Elapsed);
                }
                else
                {
                    return ExportResult.Failure("PDF export failed - unknown error");
                }
            }
            catch (Exception ex)
            {
                return ExportResult.Failure($"PDF export exception: {ex.Message}");
            }
        }

        public bool CanExport(UIDocument uidoc)
        {
            if (uidoc?.Document == null)
            {
                return false;
            }

            // Проверяем наличие листов
            FilteredElementCollector collector = new FilteredElementCollector(uidoc.Document)
                .OfClass(typeof(ViewSheet));

            return collector.Any();
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
                AlwaysUseRaster = false,
                ReplaceHalftoneWithThinLines = false
            };
        }

        private void OpenFolder(string folderPath)
        {
            try
            {
                _ = Process.Start("explorer.exe", folderPath);
            }
            catch
            {
                // Игнорируем ошибки открытия папки
            }
        }
    }
}