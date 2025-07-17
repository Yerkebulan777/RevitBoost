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

        public void ExportAllSheets(bool combineFiles = true)
        {
            List<ViewSheet> sheets = GetValidSheets();

            if (!sheets.Any())
            {
                Console.WriteLine("No printable sheets found");
                return;
            }

            PDFExportOptions pdfOptions = CreatePDFOptions(combineFiles);
            List<ElementId> sheetIds = sheets.Select(s => s.Id).ToList();

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
            return new FilteredElementCollector(_document)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(sheet => sheet.CanBePrinted && !sheet.IsTemplate && !string.IsNullOrEmpty(sheet.SheetNumber))
                .OrderBy(sheet => sheet.SheetNumber)
                .ToList();
        }

        private PDFExportOptions CreatePDFOptions(bool combineFiles)
        {
            return new PDFExportOptions
            {
                FileName = combineFiles ? "AllSheets_Combined" : "Individual_Sheets",
                Combine = combineFiles,
                ColorDepth = ColorDepthType.Color,
                ExportQuality = PDFExportQualityType.DPI600,
                HideCropBoundaries = true,
                HideReferencePlane = true,
                HideScopeBoxes = true,
                HideUnreferencedViewTags = true,
                PaperFormat = ExportPaperFormat.Default,
                StopOnError = false
            };
        }
    }
}
