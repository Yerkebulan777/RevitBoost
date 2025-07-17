using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using ExportPdfTool;
using System.IO;

namespace RevitBoost.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ExportAllSheetsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            string outputPath = @"C:\RevitExports\PDFs";

            try
            {
                _ = Directory.CreateDirectory(outputPath);
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
