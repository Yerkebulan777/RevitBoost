using Autodesk.Revit.DB;
using CommonUtils;
using RevitUtils;
using System.Diagnostics;
using System.Text;

namespace LevelAssignment
{
    public sealed class AssignmentProcessor
    {
        private readonly Document _document;
        private readonly IModuleLogger _logger;
        private readonly FloorInfoGenerator _floorInfoGenerator;
        private readonly BoundaryCalculator _boundaryCalculator;

        public AssignmentProcessor(Document document, IModuleLogger logger)
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
            StringBuilder output = new();

            HashSet<ElementId> elemIdSet = [];

            double offset = UnitManager.MmToFoot(250);
            double сlearance = UnitManager.MmToFoot(100);

            output.AppendLine("\nThe start of level assignment...");

            List<FloorInfo> floorModels = _floorInfoGenerator.GenerateFloorModels(_document);

            LevelSharedParameter = SharedParameterElement.Lookup(_document, sharedParameterGuid);

            ProjectBoundaryOutline = _boundaryCalculator.ComputeProjectBoundary(_document, ref floorModels);

            ModelCategoryFilter = new ElementMulticategoryFilter(CollectorHelper.GetModelCategoryIds(_document));

            output.AppendLine();

            if (LevelSharedParameter is null)
            {
                _logger.Warning("Shared parameter {ParameterGuid} not found", sharedParameterGuid);
                throw new InvalidOperationException($"Shared parameter {sharedParameterGuid} not found");
            }

            output.AppendLine($"Shared parameter: {LevelSharedParameter?.Name}");
            output.AppendLine($"Number of floors: {floorModels?.Count}");

            foreach (FloorInfo floor in floorModels)
            {
                try
                {
                    _ = output.AppendLine();
                    floor.AggregateLevelFilter();
                    floor.ModelCategoryFilter = ModelCategoryFilter;
                    floor.LevelSharedParameter = LevelSharedParameter;
                    floor.CreateIntersectFilter(ProjectBoundaryOutline, offset, сlearance);

                    elemIdSet = [.. floor.CreateLevelFilteredCollector(_document).ToElementIds()];

                    foreach (Element element in floor.CreateExcludedCollector(_document, elemIdSet))
                    {
                        Debug.WriteLine($"Исключающий ID: {element.Id} ");

                        if (floor.IsContained(in element))
                        {
                            bool addedSuccessfully = elemIdSet.Add(element.Id);
                            Debug.Assert(addedSuccessfully, $"Failed to add element");
                        }
                    }

                }
                catch (Exception ex)
                {
                    _ = output.AppendLine($"Error during floor processing: {ex.Message}");
                }
                finally
                {
                    output.AppendLine();
                    output.AppendLine($"✅ Floor: {floor.DisplayName} <<{floor.FloorIndex}>> ");
                    output.AppendLine($"✅ Height: {UnitManager.FootToMt(floor.Height)} м.");
                    output.AppendLine($"✅ Elevation: {UnitManager.FootToMt(floor.ProjectElevation)} м.");
                    output.AppendLine(ApplyLevelParameter(_document, elemIdSet, floor.FloorIndex));

                    floor.FloorBoundingSolid.CreateDirectShape(_document);
                }
            }

            _ = output.AppendLine("Level assignment execution completed");

            return output.ToString();
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

            _ = output.AppendLine($"Start setting floor number to {levelValue}");
            _ = output.AppendLine($"The total element count: {elemIdSet.Count}");

            InternalDefinition levelParamGuid = LevelSharedParameter.GetDefinition();

            if (!TransactionHelper.TryCreateTransaction(doc, $"SetFloorNumber", () =>
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
            }, out string error))
            {
                _ = output.AppendLine($"❌ Transaction failed: {error}");
            }

            _ = output.AppendLine($"Read-only elements: {readOnlyParameterCount}");
            _ = output.AppendLine($"Not modifiable elements: {notModifiableCount}");
            _ = output.AppendLine($"TotalLevelCount elements assigned: {assignedCount}");

            return output.ToString();
        }



    }
}
