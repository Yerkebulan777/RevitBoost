using RevitUtils;
using System.Diagnostics;
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

                    HashSet<ElementId> elemIdSet = [.. floor.CreateLevelFilteredCollector(_document).ToElementIds()];

                    elemIdSet.UnionWith(floor.CreateExcludedCollector(_document, elemIdSet).Where(i => floor.IsElementContained(in i)).Select(i => i.Id));

                    levelAssignmentCount = ApplyLevelParameter(_document, elemIdSet, floor.Index);

                    _ = result.AppendLine($"Этаж: {floor.Index} Высота этажа: {floor.Height}");

                    _ = result.AppendLine($"Общее количество всех элементов: {elemIdSet.Count}");

                    _ = result.AppendLine($"Количество эементов с назначенным этажем: {levelAssignmentCount}");
                }
                catch (Exception ex)
                {
                    Debug.Fail($"Ошибка при обработке этажа {floor.Index}: {ex.Message}");
                }
                finally
                {
                    if (levelAssignmentCount % 1000 == 0)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
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
