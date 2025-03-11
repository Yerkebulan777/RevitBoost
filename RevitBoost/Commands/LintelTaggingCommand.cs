using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using LintelMaster;
using Nice3point.Revit.Toolkit.External;

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

            LintelManager manager = new(config);

            Dictionary<SizeKey, List<LintelData>> lintels = manager.RetrieveLintelData(doc, "(перемычки)уголки_арматуры");

            TaskDialog.Show("УРА!", $"Успешно промаркировано {lintels.Count} типов перемычек.");
        }
    }



}