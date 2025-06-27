using RevitUtils;

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
            FloorAssignmentResults results = new();

            try
            {
                // Этап 1: Подготовка данных

                double offset = UnitManager.MmToFoot(250);

                double clearance = UnitManager.MmToFoot(100);

                List<Level> levels = GetValidLevels(_document);

                List<FloorInfo> floorModels = _levelCalculator.GenerateFloorModels(levels);

                Outline ProjectBoundaryOutline = _boundaryCalculator.ComputeProjectBoundary(_document, ref floorModels);

                ElementMulticategoryFilter categoryFilter = new(CollectorHelper.GetModelCategoryIds(_document));

                SharedParameterElement parameter = SharedParameterElement.Lookup(_document, targetParameterGuid);

                // Этап 2: Основной цикл(оптимизирован для параллельной обработки)

                foreach (FloorInfo floor in floorModels)
                {
                    double height = floor.Height;

                    double elevation = floor.InternalElevation;

                    LogicalOrFilter intersectFilter = CreateIntersectFilter(ProjectBoundaryOutline, elevation, height, offset, clearance);

                    LogicalAndFilter logicalAndFilter = new(categoryFilter, intersectFilter);

                    FilteredElementCollector collector = CollectorHelper.GetInstancesByFilter(_document, parameter, logicalAndFilter);

                }

                // Этап 3: Сбор и анализ элементов
                List<ElementSpatialData> spatialData = _elementAnalyzer.CalculateElementsSpatialData(targetElements);

                // Этап 4: Определение принадлежности элементов к этажам
                List<ElementFloorAssignment> assignmentResults = ProcessElementAssignments(spatialData, floorModels);

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


        private LogicalOrFilter CreateIntersectFilter(Outline boundary, double elevation, double height, double offset, double clearance)
        {
            XYZ minPoint = boundary.MinimumPoint;
            XYZ maxPoint = boundary.MaximumPoint;

            minPoint = Transform.Identity.OfPoint(new XYZ(minPoint.X, minPoint.Y, elevation + clearance - offset));

            maxPoint = Transform.Identity.OfPoint(new XYZ(maxPoint.X, maxPoint.Y, elevation + height - offset));

            Solid floorSolid = SolidHelper.CreateSolidBoxByPoint(minPoint, maxPoint, height);

            Outline outline = new(minPoint, maxPoint);

            ElementIntersectsSolidFilter solidFilter = new(floorSolid);
            BoundingBoxIntersectsFilter boundingBoxFilter = new(outline);

            return new LogicalOrFilter(boundingBoxFilter, solidFilter);
        }


        /// <summary>
        /// Определяет принадлежность каждого элемента к этажу используя комбинированную стратегию
        /// </summary>
        private List<ElementFloorAssignment> ProcessElementAssignments(List<ElementSpatialData> spatialData, List<FloorInfo> floorModels)
        {
            List<ElementFloorAssignment> assignments = [];

            foreach (ElementSpatialData elementData in spatialData)
            {
                ElementFloorAssignment assignment = new() { ElementData = elementData };

                // Стратегия 1: Использование встроенного определителя уровней
                LevelAssignmentResult levelResult = _levelDeterminator.DetermineElementLevel(elementData.Element);
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
            List<FloorInfo> sortedFloors = floors.OrderBy(f => f.InternalElevation).ToList();
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


        private FloorInfo FindFloorByLevel(List<FloorInfo> floors, Level level)
        {
            return floors.FirstOrDefault(f => f.ContainedLevels.Any(l => l.Id == level.Id));
        }

        public List<Level> GetValidLevels(Document doc, double maxHeightInMeters = 100)
        {
            double maximum = UnitManager.MmToFoot(maxHeightInMeters * 1000);
            ParameterValueProvider provider = new(new ElementId(BuiltInParameter.LEVEL_ELEV));
            FilterDoubleRule rule = new(provider, new FilterNumericLess(), maximum, 5E-3);

            return [.. new FilteredElementCollector(doc).OfClass(typeof(Level))
                .WherePasses(new ElementParameterFilter(rule)).Cast<Level>()
                .OrderBy(x => x.Elevation)];
        }

        /// <summary>
        /// Применяет результаты назначения, записывая значения в параметры элементов
        /// </summary>
        private void ApplyAssignmentResults(List<ElementFloorAssignment> assignments, Guid parameterGuid)
        {
            foreach (ElementFloorAssignment assignment in assignments.Where(a => a.AssignedFloor != null))
            {
                Parameter parameter = assignment.ElementData.Element.get_Parameter(parameterGuid);
                if (parameter != null && !parameter.IsReadOnly)
                {
                    _ = parameter.Set(assignment.AssignedFloor.Index);
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
