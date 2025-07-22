using Autodesk.Revit.DB;
using CommonUtils;
using RevitUtils;
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
        private List<FloorData> FloorDataCollection { get; set; }
        private Outline ProjectBoundaryOutline { get; set; }

        /// <summary>
        /// Выполняет полный цикл анализа и назначения элементов к этажам
        /// </summary>
        public string Execute(Guid sharedParameterGuid)
        {
            StringBuilder output = new();

            double offset = UnitManager.MmToFoot(300);
            double сlearance = UnitManager.MmToFoot(100);

            output.AppendLine("=== ASSIGNMENT OF ELEMENTS TO FLOORS ===");

            LevelSharedParameter = SharedParameterElement.Lookup(_document, sharedParameterGuid);

            if (LevelSharedParameter is null)
            {
                _logger.Warning("Shared parameter {ParameterGuid} not found", sharedParameterGuid);
                throw new InvalidOperationException($"Shared parameter {sharedParameterGuid} not found");
            }

            FloorDataCollection = _floorInfoGenerator.GenerateFloorModels(_document);

            ProjectBoundaryOutline = _boundaryCalculator.ComputeProjectBoundary(_document, FloorDataCollection);

            (List<ElementId> modelCategoryIds, _) = CategoryHelper.GetModelCategoryIds(_document);

            ModelCategoryFilter = new ElementMulticategoryFilter(modelCategoryIds);

            _ = output.AppendLine($"Shared parameter: {LevelSharedParameter?.Name}");
            _ = output.AppendLine($"Number of floors: {FloorDataCollection?.Count}");
            _ = output.AppendLine("Start process:");

            ICollection<ElementId> elementIds = null;

            foreach (FloorData floor in FloorDataCollection)
            {
                try
                {
                    output.AppendLine();

                    floor.AggregateLevelFilter();
                    floor.ModelCategoryFilter = ModelCategoryFilter;
                    floor.LevelSharedParameter = LevelSharedParameter;
                    floor.CreateIntersectFilter(ProjectBoundaryOutline, offset, сlearance);

                    elementIds = floor.CreateLevelCollector(_document).ToElementIds();

                    foreach (Element element in floor.CreateExcludedCollector(_document, elementIds))
                    {
                        if (element is FamilyInstance instance)
                        {
                            Element parent = FamilyHelper.GetParentFamily(instance);

                            if (parent is null && floor.IsContained(in element))
                            {
                                elementIds.Add(element.Id);
                            }
                            else if (parent is not null && floor.IsContained(in parent))
                            {
                                elementIds.Add(parent.Id);
                            }
                        }
                        else if (floor.IsContained(in element))
                        {
                            elementIds.Add(element.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    output.AppendLine($"Error during floor processing: {ex.Message}");
                }
                finally
                {
                    output.AppendLine();
                    output.AppendLine($"✅ Floor: {floor.DisplayName} <<{floor.FloorIndex}>> ");
                    output.AppendLine($"✅ Floor height: {UnitManager.FootToMt(floor.Height)} м.");
                    output.AppendLine($"✅ Floor elevat: {UnitManager.FootToMt(floor.ProjectElevation)} м.");
                    output.AppendLine(SetParameterValue(_document, elementIds, floor.FloorIndex));
                }
            }

            output.AppendLine("Level assignment execution completed");

            return output.ToString();
        }

        /// <summary>
        /// Устанавливает значение параметра для элементов
        /// </summary>
        public string SetParameterValue(Document doc, ICollection<ElementId> elementIds, int value)
        {
            int assignedCount = 0;
            StringBuilder output = new();

            _ = output.AppendLine($"Start setting value to {value}");

            InternalDefinition paramDefinition = LevelSharedParameter.GetDefinition();

            TransactionHelper.CreateTransaction(doc, $"SetFloorNumber", () =>
            {
                foreach (ElementId elementId in elementIds)
                {
                    Element element = doc.GetElement(elementId);

                    Parameter param = element?.get_Parameter(paramDefinition);

                    if (param is not null && !param.IsReadOnly)
                    {
                        if (param.UserModifiable && param.Set(value))
                        {
                            assignedCount++;
                            continue;
                        }

                        _ = output.AppendLine($"❌ {element.Id.IntegerValue}");
                    }
                }
            });

            _ = output.AppendLine($"Total elements assigned: {assignedCount}");

            return output.ToString();
        }



    }
}
