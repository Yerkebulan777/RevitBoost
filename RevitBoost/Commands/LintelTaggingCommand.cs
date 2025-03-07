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

            LintelManager manager = new(new GroupingConfig());

            Dictionary<SizeKey, List<LintelData>> lintels = manager.RetrieveLintelData(doc, "Перемычка");

            TaskDialog.Show("УРА!", $"Успешно промаркировано {lintels.Count} типов перемычек.");
        }
    }



}