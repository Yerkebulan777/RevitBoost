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

            _ = output.AppendLine("\nThe start of level assignment...");

            LevelSharedParameter = SharedParameterElement.Lookup(_document, sharedParameterGuid);

            if (LevelSharedParameter is null)
            {
                _logger.Warning("Shared parameter {ParameterGuid} not found", sharedParameterGuid);
                throw new InvalidOperationException($"Shared parameter {sharedParameterGuid} not found");
            }

            FloorDataCollection = _floorInfoGenerator.GenerateFloorModels(_document);

            ProjectBoundaryOutline = _boundaryCalculator.ComputeProjectBoundary(_document, FloorDataCollection);

            (List<ElementId> modelCategoryIds, string otput) = CategoryHelper.GetModelCategoryIds(_document);

            ModelCategoryFilter = new ElementMulticategoryFilter(modelCategoryIds);

            _ = output.AppendLine($"Shared parameter: {LevelSharedParameter?.Name}");
            _ = output.AppendLine($"Number of floors: {FloorDataCollection?.Count}");
            _ = output.AppendLine("Start process:");
            _ = output.AppendLine(otput);

            ICollection<ElementId> elementIds = null;

            foreach (FloorData floor in FloorDataCollection)
            {
                try
                {
                    _ = output.AppendLine();

                    floor.AggregateLevelFilter();
                    floor.ModelCategoryFilter = ModelCategoryFilter;
                    floor.LevelSharedParameter = LevelSharedParameter;
                    floor.CreateIntersectFilter(ProjectBoundaryOutline, offset, сlearance);

                    elementIds = floor.CreateLevelCollector(_document).ToElementIds();

                    foreach (Element element in floor.CreateExcludedCollector(_document, elementIds))
                    {
                        Debug.WriteLine($"Исключающий ID: {element.Id} ");

                        if (floor.IsContained(in element))
                        {
                            elementIds.Add(element.Id);
                        }
                    }

                    floor.FloorBoundingSolid.CreateDirectShape(_document);


                }
                catch (Exception ex)
                {
                    _ = output.AppendLine($"Error during floor processing: {ex.Message}");
                }
                finally
                {
                    _ = output.AppendLine();
                    _ = output.AppendLine($"✅ Floor: {floor.DisplayName} <<{floor.FloorIndex}>> ");
                    _ = output.AppendLine($"✅ Floor height: {UnitManager.FootToMt(floor.Height)} м.");
                    _ = output.AppendLine($"✅ Floor elevat: {UnitManager.FootToMt(floor.ProjectElevation)} м.");
                    _ = output.AppendLine(SetParameterValue(_document, elementIds, floor.FloorIndex));
                }
            }

            _ = output.AppendLine("Level assignment execution completed");

            return output.ToString();
        }

        /// <summary>
        /// Устанавливает значение параметра для элементов
        /// </summary>
        public string SetParameterValue(Document doc, ICollection<ElementId> elementIds, int value)
        {
            int assignedCount = 0;
            StringBuilder output = new();

            output.AppendLine($"Start setting floor number to {value}");
            output.AppendLine($"The total element count: {elementIds.Count}");

            InternalDefinition paramDefinition = LevelSharedParameter.GetDefinition();

            if (!TransactionHelper.TryCreateTransaction(doc, $"SetFloorNumber", () =>
            {
                foreach (ElementId elementId in elementIds)
                {
                    Element element = doc.GetElement(elementId);
                    Parameter param = element?.get_Parameter(paramDefinition);

                    if (param is null)
                    {
                        output.AppendLine($"❌ Parameter not found: {element.Category}");
                    }
                    else if (param.IsReadOnly)
                    {
                        output.AppendLine($"❌ Read-only parameter: {elementId.IntegerValue}");
                    }
                    else if (param.UserModifiable && param.Set(value))
                    {
                        assignedCount++;
                    }
                }
            }, out string error))
            {
                output.AppendLine($"❌ Transaction failed: {error}");
            }

            output.AppendLine($"TotalLevelCount elements assigned: {assignedCount}");

            return output.ToString();
        }



    }
}
