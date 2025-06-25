using RevitUtils;

namespace LevelAssignment
{
    internal sealed class FloorElementAssigner
    {
        public List<Level> GetValidLevels(Document doc, double maxHeightInMeters = 100)
        {
            double maximum = UnitManager.MmToFoot(maxHeightInMeters * 1000);
            ParameterValueProvider provider = new(new ElementId(BuiltInParameter.LEVEL_ELEV));
            FilterDoubleRule rule = new(provider, new FilterNumericLess(), maximum, 5E-3);

            return [.. new FilteredElementCollector(doc).OfClass(typeof(Level))
                .WherePasses(new ElementParameterFilter(rule)).Cast<Level>()
                .OrderBy(x => x.Elevation)];
        }



        public class OptimizedFloorElementAssigner
        {
            public Dictionary<FloorModel, List<Element>> AssignElementsToFloors(Document doc, List<Level> levels)
            {
                // 1. Подготовительный этап (выполняется один раз)
                var boundaryCalculator = new BoundaryCalculator(doc);
                var projectBounds = boundaryCalculator.CalculateProjectBounds(levels);

                var floorGenerator = new OptimizedFloorModelGenerator();
                var floors = floorGenerator.GetFloorModels(levels);

                var filterFactory = new FloorFilterFactory(projectBounds);
                var collector = new OptimizedElementCollector(doc);
                var verifier = new ElementFloorVerifier(doc, floors);

                var result = new Dictionary<FloorModel, List<Element>>();

                // 2. Основной цикл (оптимизирован для параллельной обработки)
                foreach (var floor in floors)
                {
                    // Первичная фильтрация (быстрая)
                    var floorFilter = filterFactory.CreateOptimizedFloorFilter(doc, floor, floors);
                    var candidateElements = collector.GetElementsForFloor(floor, floorFilter);

                    // Точная верификация (для спорных случаев)
                    var verifiedElements = candidateElements
                        .Where(element => verifier.VerifyElementBelongsToFloor(element, floor))
                        .ToList();

                    result[floor] = verifiedElements;
                }

                return result;
            }
        }




    }
}
