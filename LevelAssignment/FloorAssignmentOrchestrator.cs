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

                ICollection<ElementId> elemIds =  floor.CreateFilteredElementCollector(_document).ToElementIds();

                var logicAndFilter = new LogicalAndFilter(   modelCategoryFilter, new ExclusionFilter(elemIds));

                _ = result.AppendLine($"Найдено элементов: {elemIds.Count}");




            }

            // Допиши оптимальный алгоритм для фильтрации элементов с учетом их параметров или геометрии
            // Стоит ли делать все итерации (проверки) в одном цикле или лучше разделить на этапы?
            // Оптимально ли будет использоваться память при большом количестве элементов?

            //  Все результаты собери в StringBuilder:
            //  = targetElements.Count;
            //  = assignmentResults.Count(r => r.AssignedFloor != null);
            //  = floorModels;
            //  = true;

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




        /// <summary>
        /// Создает фильтр пересекающиеся с заданной 3D-границей и диапазоном высот.
        /// </summary>
        private LogicalOrFilter CreateIntersectFilter(Outline boundary, FloorInfo floor, double offset, double clearance)
        {
            double height = floor.Height;

            XYZ minPoint = boundary.MinimumPoint;
            XYZ maxPoint = boundary.MaximumPoint;

            double elevation = floor.InternalElevation;

            minPoint = Transform.Identity.OfPoint(new XYZ(minPoint.X, minPoint.Y, elevation + clearance - offset));

            maxPoint = Transform.Identity.OfPoint(new XYZ(maxPoint.X, maxPoint.Y, elevation + height - offset));

            Solid floorSolid = SolidHelper.CreateSolidBoxByPoint(minPoint, maxPoint, height);

            Outline outline = new(minPoint, maxPoint);

            ElementIntersectsSolidFilter solidFilter = new(floorSolid);
            BoundingBoxIntersectsFilter boundingBoxFilter = new(outline);

            return new LogicalOrFilter(boundingBoxFilter, solidFilter);
        }


        #region AI methods

        /// <summary>
        /// Обработка всех элементов
        /// </summary>
        private List<AssignmentStatus> ProcessAllElements(List<Element> elements, List<FloorInfo> floors)
        {
            List<AssignmentStatus> results = [];
            HashSet<ElementId> processed = [];

            foreach (Element element in elements)
            {
                if (processed.Contains(element.Id))
                {
                    continue;
                }

                AssignmentStatus result = AssignElementToFloor(element, floors);
                results.Add(result);
                _ = processed.Add(element.Id);
            }

            return results;
        }

        /// <summary>
        /// Назначение элемента к этажу
        /// </summary>
        private AssignmentStatus AssignElementToFloor(Element element, List<FloorInfo> floors)
        {
            AssignmentStatus result = new(element);

            // Способ 1: По параметрам уровня
            if (TryAssignByLevel(element, floors, result))
            {
                return result;
            }

            // Способ 2: По геометрии
            if (TryAssignByGeometry(element, floors, result))
            {
                return result;
            }

            // Не удалось назначить
            result.Message = "Не удалось определить этаж";
            return result;
        }

        /// <summary>
        /// Попытка назначения по параметрам уровня
        /// </summary>
        private bool TryAssignByLevel(Element element, List<FloorInfo> floors, AssignmentStatus result)
        {
            foreach (FloorInfo floor in floors)
            {
                HashSet<ElementId> levelIds = floor.ContainedLevelIds.Select(l => l.Id).ToHashSet();

                if (_levelDeterminator.IsOnLevel(element, ref levelIds))
                {
                    result.IsSuccess = true;
                    result.Method = Determination.ParameterBased;
                    result.Confidence = 0.9f;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Попытка назначения по геометрии
        /// </summary>
        private bool TryAssignByGeometry(Element element, List<FloorInfo> floors, AssignmentStatus result)
        {
            BoundingBoxXYZ bbox = element.get_BoundingBox(null);
            if (bbox == null)
            {
                return false;
            }

            XYZ center = (bbox.Min + bbox.Max) / 2;

            foreach (FloorInfo floor in floors)
            {
                if (_levelDeterminator.IsPointContained(center, floor.BoundingBox))
                {
                    result.Method = Determination.GeometricAnalysis;
                    result.Confidence = 0.7f;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Добавление статистики в результат
        /// </summary>
        private void AddStatistics(StringBuilder result, List<AssignmentStatus> assignments, List<FloorInfo> floors)
        {
            int successful = assignments.Count(a => a.IsSuccess);
            int failed = assignments.Count - successful;

            _ = result.AppendLine($"Успешно назначено: {successful}");
            _ = result.AppendLine($"Не удалось назначить: {failed}");
            _ = result.AppendLine($"Всего этажей: {floors.Count}");

            // Статистика по методам
            int byParameter = assignments.Count(a => a.Method == Determination.ParameterBased);
            int byGeometry = assignments.Count(a => a.Method == Determination.GeometricAnalysis);

            _ = result.AppendLine($"По параметрам уровня: {byParameter}");
            _ = result.AppendLine($"По геометрии: {byGeometry}");
        }

        #endregion



    }
}
