using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using CommonUtils;
using LevelAssignment;
using RevitUtils;
using System.IO;
using System.Text;

namespace RevitBoost.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class LevelAssignmentCommand : IExternalCommand
    {
        private static readonly Guid PARAMETER_GUID = new("4673f045-9574-471f-9677-ac538a9e9a2d");

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            string logPath = Path.Combine(PathHelper.GetProjectDirectory(doc.PathName), "Log");

            IModuleLogger logger = LoggerHelper.CreateCommandLogger(doc.Title, ToString(), logPath);

            using IDisposable scope = logger.BeginScope("CommandExecution", ("LevelAssignmentCommand", doc.Title));

            logger.Information("Starting LevelAssignmentCommand...");

            Dictionary<string, List<string>> groupdata;

            StringBuilder resultBuilder = new();

            if (!Validate(doc, resultBuilder))
            {
                ShowResult("Error", resultBuilder.ToString());
                return Result.Failed;
            }

            resultBuilder.AppendLine($"Loger path: {logPath}");

            groupdata = GroupHelper.UngroupAllAndSaveInfo(doc);

            LevelAssigmentExecute(doc, logger, resultBuilder);

            GroupHelper.RestoreGroups(doc, groupdata);

            return Result.Succeeded;
        }

        /// <summary>
        /// Проверяет валидность документа для выполнения операции
        /// </summary>
        private static bool Validate(Document doc, StringBuilder log)
        {
            // Проверяем наличие общего параметра в проекте
            SharedParameterElement sharedParam = SharedParameterElement.Lookup(doc, PARAMETER_GUID);

            if (sharedParam == null)
            {
                log.AppendLine("Общий параметр 'BI_этаж' не найден в проекте");
                log.AppendLine("Добавьте параметр через Управление > Общие параметры");
                return false;
            }

            log.AppendLine($"Найден параметр: {sharedParam.Name}");

            return true;
        }

        /// <summary>
        /// Выполнение операции назначения элементов к этажам
        /// </summary>
        private void LevelAssigmentExecute(Document doc, IModuleLogger logger, StringBuilder resultBuilder)
        {
            try
            {
                AssignmentProcessor orchestrator = new(doc, logger);

                _ = resultBuilder.AppendLine("=== НАЗНАЧЕНИЕ ЭЛЕМЕНТОВ К ЭТАЖАМ ===");
                _ = resultBuilder.AppendLine(orchestrator.Execute(PARAMETER_GUID));

                ShowResult("Назначение завершено", resultBuilder.ToString());
            }
            catch (Exception ex)
            {
                _ = resultBuilder.AppendLine($"Произошла ошибка: {ex.Message}");

                if (ex.InnerException != null)
                {
                    _ = resultBuilder.AppendLine($"Детали: {ex.InnerException.Message}");
                }

                ShowResult("Ошибка выполнения", resultBuilder.ToString());
            }
        }

        /// <summary>
        /// Отображает результат выполнения команды
        /// </summary>
        private void ShowResult(string title, string message)
        {
            StringHelper.CopyToClipboard(message);

            TaskDialog dialog = new(title)
            {
                MainContent = message,
                CommonButtons = TaskDialogCommonButtons.Ok
            };

            dialog.Show();
        }



    }
}