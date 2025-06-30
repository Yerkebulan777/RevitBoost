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
        private readonly LevelDeterminator _levelDeterminator;

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

                List<Element> targetElements = [];

                foreach (FloorInfo floor in floorModels)
                {
                    double height = floor.Height;

                    double elevation = floor.InternalElevation;

                    LogicalOrFilter intersectFilter = CreateIntersectFilter(ProjectBoundaryOutline, elevation, height, offset, clearance);

                    LogicalAndFilter logicalAndFilter = new(categoryFilter, intersectFilter);

                    targetElements.AddRange(CollectorHelper.GetInstancesByFilter(_document, parameter, logicalAndFilter).ToElements());
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


}
