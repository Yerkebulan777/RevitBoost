using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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

        private ElementMulticategoryFilter ModelCategoryFilter { get; set; }
        private SharedParameterElement LevelSharedParameter { get; set; }
        private Outline ProjectBoundaryOutline { get; set; }

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
                _logger.Warning("Shared parameter {ParameterGuid} not found", sharedParameterGuid);
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

                        if (floor.IsElementContained(in element) && elemIdSet.Add(element.Id))
                        {
                            Debug.WriteLine("");
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
                    result.AppendLine($"✅ Floor: {floor.DisplayName} ({floor.Index}) ");
                    result.AppendLine($"✅ Height: {UnitManager.FootToMt(floor.Height)}");
                    result.AppendLine($"✅ Elevation: {UnitManager.FootToMt(floor.ProjectElevation)}");

                    result.AppendLine(ApplyLevelParameter(_document, elemIdSet, floor.Index));

                    floor.FloorBoundingSolid.CreateDirectShape(_document);
                }
            }

            result.AppendLine("Level assignment execution completed");

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

            output.AppendLine($"✅ The total element count: {elemIdSet.Count}");

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
