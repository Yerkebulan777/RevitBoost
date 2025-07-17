using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using CommonUtils;
using LintelMaster;
using System.Diagnostics;
using System.Text;

namespace RevitBoost.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class LintelLabelingCommand : IExternalCommand
    {
        IDictionary<SizeKey, List<LintelData>> lintels;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            StringBuilder resultBuilder = new();

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                GroupingConfig config = new()
                {
                    ThickParameterName = "BI_толщина_стены",
                    WidthParameterName = "BI_проем_ширина",
                    HeightParameterName = "BI_проем_высота"
                };

                LintelManager manager = new(config);

                lintels = manager.RetrieveLintelData(doc, "(перемычки)уголки_арматуры");

                stopwatch.Stop();

                resultBuilder.AppendLine($"Успешно получено {lintels.Count} типов.");

                resultBuilder.AppendLine($"Turnaround time: {stopwatch.Elapsed.TotalMinutes:F2} min");
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
                foreach (KeyValuePair<SizeKey, List<LintelData>> group in lintels)
                {
                    resultBuilder.AppendLine($"Группа: {group.Key} ({group.Value.Count})");
                }

                TaskDialog.Show("УРА!", resultBuilder.ToString());
            }

            return Result.Succeeded;
        }


    }
}