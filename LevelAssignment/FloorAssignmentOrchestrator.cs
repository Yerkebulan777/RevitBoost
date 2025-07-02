using RevitUtils;
using System.Diagnostics;
using System.Text;

namespace LevelAssignment
{
    public sealed class FloorAssignmentOrchestrator
    {
        private int levelAssignmentCount;
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

            if (LevelSharedParameter is null)
            {
                return $"Общий параметр {sharedParameterGuid} не найден в проекте!";
            }

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
                    _ = result.AppendLine($"Количество эементов с назначенным этажем: {levelAssignmentCount}");
                    _ = result.AppendLine($"Общее количество всех элементов: {elemIdSet.Count}");

                    levelAssignmentCount = ApplyLevelParameter(_document, elemIdSet, floor.Index);

                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Устанавливает значение параметра BI_этаж для элементов
        /// </summary>
        public int ApplyLevelParameter(Document doc, HashSet<ElementId> elemIdSet, int levelValue)
        {
            int count = 0;

            using (Transaction trx = new(doc, $"Установка номера этажа {levelValue}"))
            {
                if (trx.Start() == TransactionStatus.Started)
                {
                    try
                    {
                        InternalDefinition levelParamGuid = LevelSharedParameter.GetDefinition();

                        foreach (ElementId elementId in elemIdSet)
                        {
                            Element element = doc.GetElement(elementId);
                            Parameter param = element?.get_Parameter(levelParamGuid);

                            if (param?.Set(levelValue) == true)
                            {
                                count++;
                            }
                        }

                        _ = trx.Commit();
                    }
                    catch
                    {
                        _ = trx.RollBack();
                        throw;
                    }
                }
            }

            return count;
        }



    }
}
