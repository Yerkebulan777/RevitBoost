using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using CommonUtils;
using ExportPdfTool;
using RevitUtils;
using Serilog;
using System.Diagnostics;
using System.IO;

namespace RevitBoost.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ExportToPdfCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            string outputPath = PathHelper.GetExportDirectory(doc, out string revitFilePath, "03_PDF");
            string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);

            Log.Information("Export PDF started: {FileName}", revitFileName);
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                PathHelper.EnsureDirectory(outputPath);
                RevitPdfExporter exporter = new(doc, outputPath);
                exporter.ExportAllSheets(revitFileName);

                stopwatch.Stop();

                Log.Information("Export PDF completed {ElapsedTime:F2} min", stopwatch.Elapsed.TotalMinutes);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Log.Error("Export PDF failed: {ErrorMessage}", ex.Message);
                message = $"Error during export: {ex.Message}";
                return Result.Failed;
            }
        }
    }
}