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

        private Outline ProjectBoundary { get; set; }
        private ElementMulticategoryFilter ModelCategoryFilter { get; set; }
        private SharedParameterElement LevelSharedParameter { get; set; }

        /// <summary>
        /// Выполняет полный цикл анализа и назначения элементов к этажам
        /// </summary>
        public string ExecuteFullAssignment(Guid targetParameterGuid)
        {
            StringBuilder result = new();

            double elevationOffset = UnitManager.MmToFoot(250);
            double verticalClearance = UnitManager.MmToFoot(100);

            List<Level> levels = GetValidLevels(_document);

            List<FloorInfo> floorModels = _floorInfoGenerator.GenerateFloorModels(levels);

            LevelSharedParameter = SharedParameterElement.Lookup(_document, targetParameterGuid);

            ProjectBoundary = _boundaryCalculator.ComputeProjectBoundary(_document, ref floorModels);

            ModelCategoryFilter = new ElementMulticategoryFilter(CollectorHelper.GetModelCategoryIds(_document));

            foreach (FloorInfo floor in floorModels)
            {
                floor.AggregateLevelFilter();
                floor.ModelCategoryFilter = ModelCategoryFilter;
                floor.LevelSharedParameter = LevelSharedParameter;
                floor.CreateIntersectFilter(ProjectBoundary, elevationOffset, verticalClearance);

                HashSet<ElementId> elemIdSet = [.. floor.CreateLevelFilteredCollector(_document).ToElementIds()];

                elemIdSet.UnionWith(floor.CreateExcludedCollector(_document, elemIdSet).Where(i => floor.IsElementContained(in i)).Select(i => i.Id));

                _ = result.AppendLine($"Этаж: {floor.Index} Высота этажа: {floor.Height}");

                _ = result.AppendLine($"Найдено элементов: {elemIdSet.Count}");
            }

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
