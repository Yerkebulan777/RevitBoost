using Autodesk.Revit.DB;

namespace ExportPdfTool
{
    public class RevitPdfExporter
    {
        private readonly Document _document;
        private readonly string _outputPath;

        public RevitPdfExporter(Document document, string outputPath)
        {
            _document = document;
            _outputPath = outputPath;
        }

        public void ExportAllSheets(string exportFileName)
        {
            List<ViewSheet> sheets = GetValidSheets();

            if (!sheets.Any())
            {
                Console.WriteLine("No printable sheets found");
                return;
            }

            PDFExportOptions pdfOptions = CreatePDFOptions(exportFileName);
            List<ElementId> sheetIds = [.. sheets.Select(s => s.Id)];

            try
            {
                bool success = _document.Export(_outputPath, sheetIds, pdfOptions);

                if (success)
                {
                    Console.WriteLine($"Successfully exported {sheets.Count} sheets");
                }
                else
                {
                    Console.WriteLine("Export completed with warnings");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Export failed: {ex.Message}");
            }
        }

        private List<ViewSheet> GetValidSheets()
        {
            return [.. new FilteredElementCollector(_document)
                .OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                .Where(sheet => sheet.CanBePrinted && !sheet.IsTemplate)
                .OrderBy(sheet => sheet.SheetNumber)];
        }

        private PDFExportOptions CreatePDFOptions(string fileName)
        {
            return new PDFExportOptions
            {
                Combine = true,
                FileName = fileName,
                ColorDepth = ColorDepthType.Color,
                PaperFormat = ExportPaperFormat.Default,
                ExportQuality = PDFExportQualityType.DPI300,
                HideUnreferencedViewTags = true,
                HideCropBoundaries = true,
                HideReferencePlane = true,
                HideScopeBoxes = true,
                StopOnError = false
            };
        }



    }
}
