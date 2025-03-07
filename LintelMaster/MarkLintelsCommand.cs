using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;


namespace LintelMaster
{
    /// <summary>
    /// Команда для маркировки перемычек
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class MarkLintelsCommand : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Получаем документ
                Document doc = commandData.Application.ActiveUIDocument.Document;

                LintelManager marker = new(new GroupingConfig());

                List<FamilyInstance> lintels = marker.GetInstancesByName(doc, "Перемычка");

                TaskDialog.Show("УРА!", $"Успешно промаркировано {lintels.Count} перемычек.");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }



}