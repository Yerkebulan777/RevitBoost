using RevitUtils;
using System.Text;

namespace LevelAssignment
{
    public class FloorAssignmentOrchestrator
    {
        private double elevationOffset;
        private double verticalClearance;
        private readonly Document _document;
        private readonly FloorInfoGenerator _floorInfoGenerator;
        private readonly BoundaryCalculator _boundaryCalculator;
        private readonly LevelDeterminator _levelDeterminator;
        public FloorAssignmentOrchestrator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));

            _floorInfoGenerator = new FloorInfoGenerator();
            _boundaryCalculator = new BoundaryCalculator();
            _levelDeterminator = new LevelDeterminator();
        }

        private Outline projectBoundary { get; set; }
        private ElementMulticategoryFilter modelCategoryFilter { get; set; }
        private SharedParameterElement levelSharedParameter { get; set; }

        /// <summary>
        /// Выполняет полный цикл анализа и назначения элементов к этажам
        /// </summary>
        public string ExecuteFullAssignment(Guid targetParameterGuid)
        {
            StringBuilder result = new();

            elevationOffset = UnitManager.MmToFoot(250);
            verticalClearance = UnitManager.MmToFoot(100);

            List<Level> levels = GetValidLevels(_document);

            List<FloorInfo> floorModels = _floorInfoGenerator.GenerateFloorModels(levels);

            levelSharedParameter = SharedParameterElement.Lookup(_document, targetParameterGuid);

            projectBoundary = _boundaryCalculator.ComputeProjectBoundary(_document, ref floorModels);

            modelCategoryFilter = new ElementMulticategoryFilter(CollectorHelper.GetModelCategoryIds(_document));

            foreach (FloorInfo floor in floorModels)
            {
                floor.AggregateLevelFilter();
                floor.ModelCategoryFilter = modelCategoryFilter;
                floor.LevelSharedParameter = levelSharedParameter;
                floor.CreateIntersectFilter(projectBoundary, elevationOffset, verticalClearance);

                _ = result.AppendLine($"Этаж: {floor.Index} Высота этажа: {floor.Height}");

                ICollection<ElementId> elemIds = floor.CreateLevelFilteredElementCollector(_document).ToElementIds();

                _ = result.AppendLine($"Найдено элементов: {elemIds.Count}");

                floor.ElementExclusionFilter = new ExclusionFilter(elemIds);

                floor.CreateExcludedElementsCollector(_document).ToElementIds();

            }

            // Допиши оптимальный алгоритм для фильтрации элементов с учетом их параметров или геометрии
            // Стоит ли делать все итерации (проверки) в одном цикле или лучше разделить на этапы?
            // Оптимально ли будет использоваться память при большом количестве элементов?

            return result.ToString();
        }

        /// <summary>
        /// Получает список уровней, которые имеют высоту меньше заданного максимума
        /// </summary>
        internal List<Level> GetValidLevels(Document doc, double maxHeightInMeters = 100)
        {
            double maximum = UnitManager.MmToFoot(maxHeightInMeters * 1000);
            ParameterValueProvider provider = new(new ElementId(BuiltInParameter.LEVEL_ELEV));
            FilterDoubleRule rule = new(provider, new FilterNumericLess(), maximum, 5E-3);

            return [.. new FilteredElementCollector(doc).OfClass(typeof(Level))
        .WherePasses(new ElementParameterFilter(rule)).Cast<Level>()
        .OrderBy(x => x.Elevation)];
        }


    }
}
