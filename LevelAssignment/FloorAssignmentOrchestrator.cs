using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LevelAssignment
{
    /// <summary>
    /// Главный координатор системы определения принадлежности элементов к этажам
    /// Объединяет все компоненты для выполнения полного цикла анализа и назначения
    /// </summary>
    public class FloorAssignmentOrchestrator
    {
        private readonly Document _document;
        private readonly ElementAnalyzer _elementAnalyzer;
        private readonly LevelNumberCalculator _levelCalculator;
        private readonly ProjectBoundaryCalculator _boundaryCalculator;
        private readonly ElementLevelDeterminator _levelDeterminator;

        public FloorAssignmentOrchestrator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _elementAnalyzer = new ElementAnalyzer(document);
            _levelCalculator = new LevelNumberCalculator();
            _boundaryCalculator = new ProjectBoundaryCalculator();
            _levelDeterminator = new ElementLevelDeterminator(document);
        }

        /// <summary>
        /// Выполняет полный цикл анализа и назначения элементов к этажам
        /// </summary>
        public FloorAssignmentResults ExecuteFullAssignment(Guid targetParameterGuid)
        {
            var results = new FloorAssignmentResults();

            try
            {
                // Этап 1: Подготовка данных о структуре здания
                var levels = GetValidLevels();
                var floorModels = _levelCalculator.GenerateFloorModels(levels);

                // Этап 2: Анализ пространственных границ
                _boundaryCalculator.ComputeProjectBoundary(_document, ref floorModels);

                // Этап 3: Сбор и анализ элементов
                var targetElements = _elementAnalyzer.GetElementsWithParameter(targetParameterGuid);
                var spatialData = _elementAnalyzer.CalculateElementsSpatialData(targetElements);

                // Этап 4: Определение принадлежности элементов к этажам
                var assignmentResults = ProcessElementAssignments(spatialData, floorModels);

                // Этап 5: Применение результатов
                ApplyAssignmentResults(assignmentResults, targetParameterGuid);

                results.ProcessedElements = targetElements.Count;
                results.SuccessfulAssignments = assignmentResults.Count(r => r.AssignedFloor != null);
                results.FloorModels = floorModels;
                results.IsSuccess = true;
            }
            catch (Exception ex)
            {
                results.IsSuccess = false;
                results.ErrorMessage = ex.Message;
            }

            return results;
        }

        /// <summary>
        /// Определяет принадлежность каждого элемента к этажу используя комбинированную стратегию
        /// </summary>
        private List<ElementFloorAssignment> ProcessElementAssignments(
            List<ElementSpatialData> spatialData,
            List<FloorInfo> floorModels)
        {
            var assignments = new List<ElementFloorAssignment>();

            foreach (var elementData in spatialData)
            {
                var assignment = new ElementFloorAssignment { ElementData = elementData };

                // Стратегия 1: Использование встроенного определителя уровней
                var levelResult = _levelDeterminator.DetermineElementLevel(elementData.Element);
                if (levelResult.AssignedLevel != null)
                {
                    assignment.AssignedFloor = FindFloorByLevel(floorModels, levelResult.AssignedLevel);
                    assignment.Method = AssignmentMethod.LevelDeterminator;
                    assignment.Confidence = levelResult.Confidence;
                }

                // Стратегия 2: Геометрический анализ (если первая стратегия неуспешна)
                if (assignment.AssignedFloor == null)
                {
                    assignment.AssignedFloor = DetermineFloorByGeometry(elementData, floorModels);
                    assignment.Method = AssignmentMethod.GeometricAnalysis;
                    assignment.Confidence = 0.7;
                }

                // Стратегия 3: Пространственное пересечение (резервная стратегия)
                if (assignment.AssignedFloor == null && _boundaryCalculator.ProjectBoundaryOutline != null)
                {
                    assignment.AssignedFloor = DetermineFloorByIntersection(elementData, floorModels);
                    assignment.Method = AssignmentMethod.SpatialIntersection;
                    assignment.Confidence = 0.5;
                }

                assignments.Add(assignment);
            }

            return assignments;
        }

        /// <summary>
        /// Определяет этаж на основе геометрического анализа высоты элемента
        /// </summary>
        private FloorInfo DetermineFloorByGeometry(ElementSpatialData elementData, List<FloorInfo> floors)
        {
            var sortedFloors = floors.OrderBy(f => f.InternalElevation).ToList();
            double elementZ = elementData.MinZ;

            // Поиск подходящего этажа по высоте
            for (int i = 0; i < sortedFloors.Count - 1; i++)
            {
                double currentFloorHeight = sortedFloors[i].InternalElevation * 304.8; // Конвертация в мм
                double nextFloorHeight = sortedFloors[i + 1].InternalElevation * 304.8;

                if (elementZ >= currentFloorHeight && elementZ < nextFloorHeight)
                {
                    return sortedFloors[i];
                }
            }

            // Элемент выше последнего этажа
            if (elementZ >= sortedFloors.Last().InternalElevation * 304.8)
            {
                return sortedFloors.Last();
            }

            // Элемент ниже первого этажа
            return sortedFloors.First();
        }

        /// <summary>
        /// Определяет этаж на основе пространственного пересечения с границами проекта
        /// </summary>
        private FloorInfo DetermineFloorByIntersection(ElementSpatialData elementData, List<FloorInfo> floors)
        {
            // Реализация анализа пересечения с использованием BoundaryFilterFactory
            // Проверяем пересечение элемента с каждым этажом
            foreach (var floor in floors.OrderBy(f => Math.Abs(f.InternalElevation * 304.8 - elementData.MinZ)))
            {
                var filter = BoundaryFilterFactory.CreateIntersectFilter(
                    floor, _boundaryCalculator.ProjectBoundaryOutline, 0, 100);

                // Если элемент попадает в фильтр - он принадлежит этому этажу
                var collector = new FilteredElementCollector(_document, new[] { elementData.Element.Id });
                if (collector.WherePasses(filter).Any())
                {
                    return floor;
                }
            }

            return null;
        }

        private FloorInfo FindFloorByLevel(List<FloorInfo> floors, Level level)
        {
            return floors.FirstOrDefault(f => f.ContainedLevels.Any(l => l.Id == level.Id));
        }

        private List<Level> GetValidLevels()
        {
            return new FilteredElementCollector(_document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();
        }

        /// <summary>
        /// Применяет результаты назначения, записывая значения в параметры элементов
        /// </summary>
        private void ApplyAssignmentResults(List<ElementFloorAssignment> assignments, Guid parameterGuid)
        {
            foreach (var assignment in assignments.Where(a => a.AssignedFloor != null))
            {
                var parameter = assignment.ElementData.Element.get_Parameter(parameterGuid);
                if (parameter != null && !parameter.IsReadOnly)
                {
                    parameter.Set(assignment.AssignedFloor.Index);
                }
            }
        }
    }

    // Вспомогательные классы для результатов
    public class FloorAssignmentResults
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public int ProcessedElements { get; set; }
        public int SuccessfulAssignments { get; set; }
        public List<FloorInfo> FloorModels { get; set; }
    }

    public class ElementFloorAssignment
    {
        public ElementSpatialData ElementData { get; set; }
        public FloorInfo AssignedFloor { get; set; }
        public AssignmentMethod Method { get; set; }
        public double Confidence { get; set; }
    }

    public enum AssignmentMethod
    {
        LevelDeterminator,
        GeometricAnalysis,
        SpatialIntersection,
        Failed
    }
}
