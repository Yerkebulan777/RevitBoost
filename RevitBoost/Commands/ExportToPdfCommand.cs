using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using CommonUtils;
using ExportPdfTool;
using RevitUtils;
using System.Diagnostics;
using System.IO;
using System.Text;

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

            IModuleLogger logger = ModuleLogger.Create(doc, typeof(ExportToPdfCommand));

            using var scope = logger.BeginScope("CommandExecution");

            logger.Information("ExportToPdfCommand...");

            StringBuilder resultBuilder = new();

            resultBuilder.AppendLine($"Loger path: {logger.LogFilePath}");

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                PathHelper.EnsureDirectory(outputPath);
                RevitPdfBatchExporter exporter = new(doc, outputPath);
                resultBuilder.AppendLine(exporter.ExportAllSheets(revitFileName));
                resultBuilder.AppendLine($"Turnaround time: {stopwatch.Elapsed.TotalMinutes:F2} min");

                stopwatch.Stop();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                resultBuilder.AppendLine($"Exception: {ex.Message}");

                if (ex.InnerException != null)
                {
                    resultBuilder.AppendLine($"Details: {ex.InnerException.Message}");
                }

                message = resultBuilder.ToString();
                StringHelper.CopyToClipboard(message);
                return Result.Failed;
            }
            finally
            {
                logger.Information(resultBuilder.ToString());
                DialogHelper.ShowInfo("Export PDF", resultBuilder.ToString());
            }
        }



    }
}