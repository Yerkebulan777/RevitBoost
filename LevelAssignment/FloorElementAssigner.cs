using RevitUtils;

namespace LevelAssignment
{
    internal sealed class FloorElementAssigner
    {

        public class OptimizedFloorElementAssigner
        {
            //public Dictionary<FloorInfo, List<Element>> AssignElementsToFloors(Document _document, List<Level> levels)
            //{
            //    // 1. Подготовительный этап (выполняется один раз)
            //    var boundaryCalculator = new ProjectBoundaryCalculator();
            //    var projectBounds = boundaryCalculator.CalculateProjectBounds(levels);

            //    var floorGenerator = new OptimizedFloorModelGenerator();
            //    var floors = floorGenerator.GetFloorModels(levels);

            //    var filterFactory = new FloorFilterFactory(projectBounds);
            //    var collector = new OptimizedElementCollector(_document);
            //    var verifier = new ElementFloorVerifier(_document, floors);

            //    var result = new Dictionary<FloorInfo, List<Element>>();

            //    // 2. Основной цикл (оптимизирован для параллельной обработки)
            //    foreach (var floor in floors)
            //    {
            //        // Первичная фильтрация (быстрая)
            //        var floorFilter = filterFactory.CreateOptimizedFloorFilter(_document, floor, floors);
            //        var candidateElements = collector.GetElementsForFloor(floor, floorFilter);

            //        // Точная верификация (для спорных случаев)
            //        var verifiedElements = candidateElements
            //            .Where(element => verifier.VerifyElementBelongsToFloor(element, floor))
            //            .ToList();

            //        result[floor] = verifiedElements;
            //    }

            //    return result;
            //}

        }





    }
}
