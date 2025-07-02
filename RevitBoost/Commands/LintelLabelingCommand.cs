using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LintelMaster;
using System.Text;

namespace RevitBoost.Commands
{
    /// <summary>
    /// Команда для маркировки перемычек
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class LintelLabelingCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            GroupingConfig config = new()
            {
                ThickParameterName = "BI_толщина_стены",
                WidthParameterName = "BI_проем_ширина",
                HeightParameterName = "BI_проем_высота"
            };

            StringBuilder stringBuilder = new();

            LintelManager manager = new(config);

            IDictionary<SizeKey, List<LintelData>> lintels = manager.RetrieveLintelData(doc, "(перемычки)уголки_арматуры");

            stringBuilder.AppendLine($"Успешно получено {lintels.Count} типов.");

            foreach (KeyValuePair<SizeKey, List<LintelData>> group in lintels)
            {
                stringBuilder.AppendLine($"Группа: {group.Key} ({group.Value.Count})");
            }

            TaskDialog.Show("УРА!", stringBuilder.ToString());

            return Result.Succeeded;
        }
    }



}