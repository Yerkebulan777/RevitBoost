﻿using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using CommonUtils;
using LevelAssignment;
using RevitUtils;
using System.Text;

namespace RevitBoost.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class LevelAssignmentCommand : IExternalCommand
    {
        private Dictionary<string, List<string>> ungroupedGroupInfo { get; set; }

        private static readonly Guid PARAMETER_GUID = new("4673f045-9574-471f-9677-ac538a9e9a2d");

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            IModuleLogger logger = ModuleLogger.Create(doc, typeof(LevelAssignmentCommand));

            using IDisposable scope = logger.BeginScope("CommandExecution");

            logger.Information("Starting LevelAssignmentCommand...");

            StringBuilder resultBuilder = new();

            resultBuilder.AppendLine($"Loger path: {logger.LogFilePath}");

            if (!ParameterHelper.ValidateSharedParameter(doc, PARAMETER_GUID, resultBuilder))
            {
                DialogHelper.ShowError("Error", resultBuilder.ToString());
                return Result.Failed;
            }

            try
            {
                AssignmentProcessor orchestrator = new(doc, logger);
                ungroupedGroupInfo = GroupHelper.UngroupAllAndSaveInfo(doc);
                resultBuilder.AppendLine(orchestrator.Execute(PARAMETER_GUID));
                GroupHelper.RestoreGroups(doc, ungroupedGroupInfo);
            }
            catch (Exception ex)
            {
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
                DialogHelper.ShowInfo("Completed", resultBuilder.ToString());
            }

            return Result.Succeeded;
        }



    }
}