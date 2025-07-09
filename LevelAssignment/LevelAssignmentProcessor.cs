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

            _ = result.AppendLine($"TotalLevels number of floors: {floorModels?.Count}");
            _ = result.AppendLine($"General parameter: {LevelSharedParameter?.Name}");

            foreach (FloorInfo floor in floorModels)
            {
                try
                {
                    _ = result.AppendLine();
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
                    _ = result.AppendLine($"Error during floor processing: {ex.Message}");
                }
                finally
                {
                    _ = result.AppendLine();
                    _ = result.AppendLine($"Floor: {floor.DisplayName} ({floor.Index}) ");
                    _ = result.AppendLine($"Floor height: {UnitManager.FootToMt(floor.Height)}");
                    _ = result.AppendLine($"DisplayElevation: {UnitManager.FootToMt(floor.ProjectElevation)}");
                    _ = result.AppendLine($"The total number of all elements found:{elemIdSet.Count}");
                    _ = result.AppendLine(ApplyLevelParameter(_document, elemIdSet, floor.Index));

                    floor.FloorBoundingSolid.CreateDirectShape(_document);
                }
            }

            _ = result.AppendLine("Level assignment execution completed");

            return result.ToString();
        }

        /// <summary>
        /// Устанавливает значение параметра для элементов
        /// </summary>
        public string ApplyLevelParameter(Document doc, HashSet<ElementId> elemIdSet, int levelValue)
        {
            int assignedCount = 0;
            int notModifiableCount = 0;
            int readOnlyParameterCount = 0;

            StringBuilder output = new();

            InternalDefinition levelParamGuid = LevelSharedParameter.GetDefinition();

            _ = output.AppendLine($"Shared parameter: {levelParamGuid?.Name}");

            TransactionHelper.CreateTransaction(doc, $"SetFloorNumber", () =>
            {
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

                        _ = output.AppendLine($"❌ Failed element {element.UniqueId}");
                    }
                }
            });

            _ = output.AppendLine($"Read-only elements: {readOnlyParameterCount}");
            _ = output.AppendLine($"Not modifiable elements: {notModifiableCount}");
            _ = output.AppendLine($"TotalLevels elements assigned: {assignedCount}");

            _logger.Debug(output.ToString());

            return output.ToString();
        }



    }
}
