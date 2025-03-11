using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using LintelMaster;
using Nice3point.Revit.Toolkit.External;
using System.Text;

namespace RevitBoost.Commands
{
    /// <summary>
    /// Команда для маркировки перемычек
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class LintelTaggingCommand : ExternalCommand
    {
        public override void Execute()
        {
            Document doc = Document;

            GroupingConfig config = new()
            {
                ThickParameterName = "BI_толщина_стены",
                WidthParameterName = "BI_проем_ширина",
                HeightParameterName = "BI_проем_высота"
            };

            StringBuilder stringBuilder = new();

            LintelManager manager = new(config);

            Dictionary<SizeKey, List<LintelData>> lintels = manager.RetrieveLintelData(doc, "(перемычки)уголки_арматуры");

            stringBuilder.AppendLine($"Успешно промаркировано {lintels.Count} типов перемычек.");

            foreach (KeyValuePair<SizeKey, List<LintelData>> group in lintels)
            {
                stringBuilder.AppendLine($"Группа: {group.Key}");

                foreach (LintelData lintel in group.Value)
                {
                    stringBuilder.AppendLine($"\t{lintel.Instance.Name}");
                }
            }

            TaskDialog.Show("УРА!",  stringBuilder.ToString());
        }
    }



}