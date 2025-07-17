using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using CommonUtils;
using ExportPdfTool;
using RevitUtils;
using System.IO;

namespace RevitBoost.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ExportAllSheetsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            string outputPath = RevitPathHelper.GetExportDirectory(doc, "03_PDF", out string revitFilePath);
            string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);

            try
            {
                PathHelper.EnsureDirectory(outputPath);
                RevitPdfExporter exporter = new(doc, outputPath);
                exporter.ExportAllSheets(revitFileName);

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
