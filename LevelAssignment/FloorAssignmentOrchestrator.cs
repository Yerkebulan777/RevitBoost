using RevitUtils;
using System.Text;

namespace LevelAssignment
{
    public sealed class FloorAssignmentOrchestrator
    {
        private readonly int levelAssignmentCount;
        private readonly Document _document;
        private readonly FloorInfoGenerator _floorInfoGenerator;
        private readonly BoundaryCalculator _boundaryCalculator;

        public FloorAssignmentOrchestrator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));

            _floorInfoGenerator = new FloorInfoGenerator();
            _boundaryCalculator = new BoundaryCalculator();
        }


        private Outline ProjectBoundaryOutline { get; set; }
        private ElementMulticategoryFilter ModelCategoryFilter { get; set; }
        private SharedParameterElement LevelSharedParameter { get; set; }


        /// <summary>
        /// Выполняет полный цикл анализа и назначения элементов к этажам
        /// </summary>
        public string ExecuteFullAssignment(Guid sharedParameterGuid)
        {
            StringBuilder result = new();

            HashSet<ElementId> elemIdSet = [];

            double elevationOffset = UnitManager.MmToFoot(250);
            double verticalClearance = UnitManager.MmToFoot(100);

            List<FloorInfo> floorModels = _floorInfoGenerator.GenerateFloorModels(_document);

            LevelSharedParameter = SharedParameterElement.Lookup(_document, sharedParameterGuid);

            ProjectBoundaryOutline = _boundaryCalculator.ComputeProjectBoundary(_document, ref floorModels);

            ModelCategoryFilter = new ElementMulticategoryFilter(CollectorHelper.GetModelCategoryIds(_document));

            _ = result.AppendLine($"Общий параметр найден: {LevelSharedParameter?.Name}");
            _ = result.AppendLine($"Общее количество этажей: {floorModels?.Count}");

            foreach (FloorInfo floor in floorModels)
            {
                try
                {
                    floor.AggregateLevelFilter();
                    floor.ModelCategoryFilter = ModelCategoryFilter;
                    floor.LevelSharedParameter = LevelSharedParameter;
                    floor.CreateIntersectFilter(ProjectBoundaryOutline, elevationOffset, verticalClearance);

                    elemIdSet = [.. floor.CreateLevelFilteredCollector(_document).ToElementIds()];

#pragma warning disable S3267 // Loops should be simplified with "LINQ" expressions

                    foreach (Element element in floor.CreateExcludedCollector(_document, elemIdSet))
                    {
                        if (floor.IsElementContained(in element))
                        {
                            _ = elemIdSet.Add(element.Id);
                        }
                    }

#pragma warning restore S3267 // Loops should be simplified with "LINQ" expressions

                }
                catch (Exception ex)
                {
                    _ = result.AppendLine($"Ошибка при обработке этажа: {ex.Message}");
                }
                finally
                {
                    _ = result.AppendLine($"Этаж: {floor.DisplayName} {floor.Index} Высота этажа: {floor.Height}");

                    _ = result.AppendLine($"Общее количество всех найденных элементов: {elemIdSet.Count}");

                    _ = result.AppendLine(ApplyLevelParameter(_document, elemIdSet, floor.Index));
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Устанавливает значение параметра BI_этаж для элементов
        /// </summary>
        public string ApplyLevelParameter(Document doc, HashSet<ElementId> elemIdSet, int levelValue)
        {
            int assignedCount = 0;

            StringBuilder result = new();

            using (Transaction trx = new(doc, $"Setting the floor number {levelValue}"))
            {
                if (TransactionStatus.Started == trx?.Start())
                {
                    try
                    {
                        InternalDefinition levelParamGuid = LevelSharedParameter.GetDefinition();

                        result.AppendLine($"Shared parameter: {levelParamGuid?.Name}");

                        foreach (ElementId elementId in elemIdSet)
                        {
                            Element element = doc.GetElement(elementId);
                            Parameter param = element?.get_Parameter(levelParamGuid);

                            if (param is not null && !param.IsReadOnly)
                            {
                                if (param.Set(levelValue))
                                {
                                    assignedCount++;
                                }
                                else
                                {
                                    string elementName = element.Name;
                                    string category = element.Category.Name;
                                    result.AppendLine($"Failed for element {elementName} in category {category}");
                                }
                            }
                        }

                        _ = trx.Commit();
                    }
                    catch (Exception ex)
                    {
                        _ = trx.RollBack();
                        _ = result.AppendLine($"Error during transaction: {ex.Message}");
                    }
                }
            }

            result.AppendLine($"Total elements assigned to level {levelValue}: {assignedCount}");

            return result.ToString();
        }



    }
}
