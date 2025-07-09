using Autodesk.Revit.DB;
using CommonUtils;
using RevitUtils;
using System.Diagnostics;
using System.Text;

namespace LevelAssignment
{
    public sealed class LevelAssignmentProcessor
    {
        private readonly Document _document;
        private readonly IModuleLogger _logger;
        private readonly FloorInfoGenerator _floorInfoGenerator;
        private readonly BoundaryCalculator _boundaryCalculator;

        public LevelAssignmentProcessor(Document document, IModuleLogger logger)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _floorInfoGenerator = new FloorInfoGenerator(_logger);
            _boundaryCalculator = new BoundaryCalculator(_logger);
        }

        private Outline ProjectBoundaryOutline { get; set; }
        private ElementMulticategoryFilter ModelCategoryFilter { get; set; }
        private SharedParameterElement LevelSharedParameter { get; set; }

        /// <summary>
        /// Выполняет полный цикл анализа и назначения элементов к этажам
        /// </summary>
        public string Execute(Guid sharedParameterGuid)
        {
            StringBuilder result = new();

            HashSet<ElementId> elemIdSet = [];

            double offset = UnitManager.MmToFoot(250);
            double сlearance = UnitManager.MmToFoot(100);

            result.AppendLine("Starting level assignment process...");

            List<FloorInfo> floorModels = _floorInfoGenerator.GenerateFloorModels(_document);

            LevelSharedParameter = SharedParameterElement.Lookup(_document, sharedParameterGuid);

            ProjectBoundaryOutline = _boundaryCalculator.ComputeProjectBoundary(_document, ref floorModels);

            ModelCategoryFilter = new ElementMulticategoryFilter(CollectorHelper.GetModelCategoryIds(_document));

            if (LevelSharedParameter is null)
            {
                _logger.Warning("Parameter {ParameterGuid} not found", sharedParameterGuid);
                throw new InvalidOperationException($"Shared parameter {sharedParameterGuid} not found");
            }

            result.AppendLine($"TotalLevels number of floors: {floorModels?.Count}");
            result.AppendLine($"General parameter: {LevelSharedParameter?.Name}");

            foreach (FloorInfo floor in floorModels)
            {
                try
                {
                    result.AppendLine();
                    floor.AggregateLevelFilter();
                    floor.ModelCategoryFilter = ModelCategoryFilter;
                    floor.LevelSharedParameter = LevelSharedParameter;
                    floor.CreateIntersectFilter(ProjectBoundaryOutline, offset, сlearance);

                    elemIdSet = [.. floor.CreateLevelFilteredCollector(_document).ToElementIds()];

                    foreach (Element element in floor.CreateExcludedCollector(_document, elemIdSet))
                    {
                        Debug.WriteLine($"Element: {element.Name}");

                        if (floor.IsElementContained(in element))
                        {
                            _ = elemIdSet.Add(element.Id);
                        }
                    }

                }
                catch (Exception ex)
                {
                    result.AppendLine($"Error during floor processing: {ex.Message}");
                }
                finally
                {
                    result.AppendLine();
                    result.AppendLine($"Floor: {floor.DisplayName} ({floor.Index}) ");
                    result.AppendLine($"Floor height: {UnitManager.FootToMt(floor.Height)}");
                    result.AppendLine($"DisplayElevation: {UnitManager.FootToMt(floor.ProjectElevation)}");
                    result.AppendLine($"The total number of all elements found:{elemIdSet.Count}");
                    result.AppendLine(ApplyLevelParameter(_document, elemIdSet, floor.Index));

                    floor.FloorBoundingSolid.CreateDirectShape(_document);
                }
            }

            result.AppendLine("Level assignment execution completed");

            return result.ToString();
        }

        /// <summary>
        /// Устанавливает значение параметра BI_этаж для элементов
        /// </summary>
        public string ApplyLevelParameter(Document doc, HashSet<ElementId> elemIdSet, int levelValue)
        {
            int assignedCount = 0;
            int notModifiableCount = 0;
            int readOnlyParameterCount = 0;

            StringBuilder result = new();

            using (Transaction trx = new(doc, $"Setting the floor number {levelValue}"))
            {
                if (TransactionStatus.Started == trx?.Start())
                {
                    try
                    {
                        InternalDefinition levelParamGuid = LevelSharedParameter.GetDefinition();

                        _ = result.AppendLine($"Shared parameter: {levelParamGuid?.Name}");

                        foreach (ElementId elementId in elemIdSet)
                        {
                            Element element = doc.GetElement(elementId);
                            Parameter param = element?.get_Parameter(levelParamGuid);

                            if (param is not null)
                            {
                                if (param.IsReadOnly)
                                {
                                    readOnlyParameterCount++;
                                    continue;
                                }

                                if (!param.UserModifiable)
                                {
                                    notModifiableCount++;
                                    continue;
                                }

                                if (param.Set(levelValue))
                                {
                                    assignedCount++;
                                    continue;
                                }

                                string elementName = element.Name;
                                string category = element.Category.Name;

                                _ = result.AppendLine($"Failed to set parameter for element {elementName} in category {category}");

                            }
                            else
                            {
                                _ = result.AppendLine($"Parameter not found for element {element.Name}");
                            }
                        }

                        if (TransactionStatus.Committed != trx.Commit())
                        {
                            _ = result.AppendLine("Transaction could not be committed");
                        }
                        else
                        {
                            _logger.Information("Set level {LevelValue} for {AssignedCount} elements", levelValue, assignedCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Transaction error for level {LevelValue}", levelValue);
                        _ = result.AppendLine($"Error during transaction: {ex.Message}");
                    }
                    finally
                    {
                        if (!trx.HasEnded())
                        {
                            _ = trx.RollBack();
                        }

                        _ = result.AppendLine($"TotalLevels elements assigned: {assignedCount}");
                        _ = result.AppendLine($"Read-only elements: {readOnlyParameterCount}");
                        _ = result.AppendLine($"Not modifiable elements: {notModifiableCount}");
                    }
                }
            }

            _logger.Debug(result.ToString());

            return result.ToString();
        }



    }
}
