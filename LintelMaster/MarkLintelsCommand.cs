using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using LintelMaster;


namespace RevitBIMTool.Commands
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
                string familyName = "Перемычка";

                // Получаем документ
                Document doc = commandData.Application.ActiveUIDocument.Document;

                // Создаем конфигурацию
                MarkConfig config = new MarkConfig
                {
                    MarkParam = "BI_марка_изделия",
                    ThickParam = "Толщина стены",
                    WidthParam = "Ширина проема",
                    HeightParam = "Высота",
                };

                // Создаем маркировщик
                LintelMarker marker = new LintelMarker(doc, config);

                // Находим перемычки
                List<FamilyInstance> lintels = marker.FindByFamilyName(familyName);

                TaskDialog.Show("Успех", $"Успешно промаркировано {lintels.Count} перемычек.");

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