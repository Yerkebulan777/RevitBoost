using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using CommonUtils;
using ExportPdfTool;
using RevitUtils;

namespace RevitBoost.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ExportAllSheetsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            string outputPath = RevitPathHelper.DetermineExportDirectory(doc, "03_PDF", out string revitFilePath);

            try
            {
                PathHelper.EnsureDirectory(outputPath);
                RevitPdfExporter exporter = new(doc, outputPath);
                exporter.ExportAllSheets(doc.Title);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Error during export: {ex.Message}";
                return Result.Failed;
            }
        }


    }
}
