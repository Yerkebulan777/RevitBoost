using RevitUtils;
using System.Text;

namespace LevelAssignment
{
    public sealed class FloorAssignmentOrchestrator
    {
        private readonly Document _document;
        private readonly FloorInfoGenerator _floorInfoGenerator;
        private readonly BoundaryCalculator _boundaryCalculator;

        public FloorAssignmentOrchestrator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));

            _floorInfoGenerator = new FloorInfoGenerator();
            _boundaryCalculator = new BoundaryCalculator();
        }

        private int levelAssignmentCount = 0;
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

            foreach (FloorInfo floor in floorModels)
            {
                try
                {
                    floor.AggregateLevelFilter();
                    floor.ModelCategoryFilter = ModelCategoryFilter;
                    floor.LevelSharedParameter = LevelSharedParameter;
                    floor.CreateIntersectFilter(ProjectBoundaryOutline, elevationOffset, verticalClearance);

                    elemIdSet = [.. floor.CreateLevelFilteredCollector(_document).ToElementIds()];

                    elemIdSet.UnionWith(floor.CreateExcludedCollector(_document, elemIdSet).Where(i => floor.IsElementContained(in i)).Select(i => i.Id));

                    levelAssignmentCount = ApplyLevelParameter(_document, elemIdSet, floor.Index);
                }
                catch (Exception ex)
                {
                    result.AppendLine($"Ошибка при обработке этажа: {ex.Message}");
                }
                finally
                {
                    result.AppendLine($"Этаж: {floor.DisplayName} {floor.Index} Высота этажа: {floor.Height}");
                    result.AppendLine($"Количество эементов с назначенным этажем: {levelAssignmentCount}");
                    result.AppendLine($"Общее количество всех элементов: {elemIdSet.Count}");

                    // Очистка памяти каждые 1000 назначений
                    if (levelAssignmentCount % 1000 == 0)
                    {
                        GC.Collect();
                        Thread.Sleep(100);
                        GC.WaitForPendingFinalizers();
                        result.AppendLine($"Выполняется сбор мусора...");
                    }
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Устанавливает значение параметра BI_этаж для элементов
        /// </summary>
        public int ApplyLevelParameter(Document doc, HashSet<ElementId> elemIdSet, in float levelValue)
        {
            int count = 0;

            using Transaction trx = new(doc);

            TransactionStatus status = trx.Start($"Установка номера этажа {levelValue}");

            InternalDefinition levelParamGuid = LevelSharedParameter.GetDefinition();

            if (status == TransactionStatus.Started)
            {
                foreach (ElementId elementId in elemIdSet)
                {
                    Element element = doc.GetElement(elementId);

                    Parameter param = element?.get_Parameter(levelParamGuid);

                    if (param is not null && !param.IsReadOnly && param.Set(levelValue))
                    {
                        element.Dispose();
                        count++;
                    }
                }
            }

            return count;
        }



    }
}
