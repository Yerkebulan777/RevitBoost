using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using LevelAssignment;
using Nice3point.Revit.Toolkit.External;
using System.Text;

namespace RevitBoost.Commands
{
    /// <summary>
    /// Команда для автоматического назначения элементов к этажам
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class LevelAssignmentCommand : ExternalCommand
    {
        private static readonly Guid BI_FLOOR_PARAMETER_GUID = new("12345678-1234-1234-1234-123456789ABC");

        public override void Execute()
        {
            Document doc = Document;
            StringBuilder resultBuilder = new();

            try
            {
                // Проверяем валидность документа
                if (!Validate(doc, resultBuilder))
                {
                    ShowResult("Ошибка валидации", resultBuilder.ToString());
                    return;
                }

                // Создаём оркестратор для назначения этажей
                LevelAssignmentProcessor orchestrator = new(doc);

                // Выполняем полное назначение элементов к этажам
                _ = resultBuilder.AppendLine("=== НАЗНАЧЕНИЕ ЭЛЕМЕНТОВ К ЭТАЖАМ ===");
                _ = resultBuilder.AppendLine(orchestrator.Execute(BI_FLOOR_PARAMETER_GUID));

                ShowResult("Назначение завершено", resultBuilder.ToString());
            }
            catch (Exception ex)
            {
                // Обрабатываем ошибки gracefully
                _ = resultBuilder.AppendLine($"Произошла ошибка: {ex.Message}");

                if (ex.InnerException != null)
                {
                    _ = resultBuilder.AppendLine($"Детали: {ex.InnerException.Message}");
                }

                ShowResult("Ошибка выполнения", resultBuilder.ToString());
            }
        }

        /// <summary>
        /// Проверяет валидность документа для выполнения операции
        /// </summary>
        private bool Validate(Document doc, StringBuilder log)
        {
            // Проверяем наличие общего параметра в проекте
            SharedParameterElement sharedParam = SharedParameterElement.Lookup(doc, BI_FLOOR_PARAMETER_GUID);
            if (sharedParam == null)
            {
                _ = log.AppendLine("Общий параметр 'BI_этаж' не найден в проекте");
                _ = log.AppendLine("Добавьте параметр через Управление > Общие параметры");
                return false;
            }

            _ = log.AppendLine($"Найден параметр: {sharedParam.Name}");

            return true;
        }

        /// <summary>
        /// Отображает результат выполнения команды
        /// </summary>
        private void ShowResult(string title, string message)
        {
            TaskDialog dialog = new(title)
            {
                MainContent = message,
                CommonButtons = TaskDialogCommonButtons.Ok
            };

            _ = dialog.Show();
        }
    }
}