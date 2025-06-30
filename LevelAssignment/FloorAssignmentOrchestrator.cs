using RevitUtils;
using System.Text;

namespace LevelAssignment
{
    /// <summary>
    /// Главный координатор системы определения принадлежности элементов к этажам
    /// Объединяет все компоненты для выполнения полного цикла анализа и назначения
    /// </summary>
    public class FloorAssignmentOrchestrator
    {
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

        /// <summary>
        /// Выполняет полный цикл анализа и назначения элементов к этажам
        /// </summary>
        public string ExecuteFullAssignment(Guid targetParameterGuid)
        {
            StringBuilder stringBuilder = new();

            // Этап 1: Подготовка данных

            double offset = UnitManager.MmToFoot(250);

            double clearance = UnitManager.MmToFoot(100);

            List<Level> levels = GetValidLevels(_document);

            List<FloorInfo> floorModels = _floorInfoGenerator.GenerateFloorModels(levels);

            Outline ProjectBoundary = _boundaryCalculator.ComputeProjectBoundary(_document, ref floorModels);

            ElementMulticategoryFilter categoryFilter = new(CollectorHelper.GetModelCategoryIds(_document));

            SharedParameterElement parameter = SharedParameterElement.Lookup(_document, targetParameterGuid);

            // Этап 2: Основной цикл(надо оптимизировать для параллельной обработки)

            List<Element> targetElements = [];

            foreach (FloorInfo floor in floorModels)
            {
                double height = floor.Height;

                double elevation = floor.InternalElevation;

                LogicalOrFilter intersectFilter = CreateIntersectFilter(ProjectBoundary, elevation, height, offset, clearance);

                IList<Element> intersectedElements = GetFilteredElements(_document, parameter, new LogicalAndFilter(categoryFilter, intersectFilter));

                targetElements.AddRange(intersectedElements);



            }

            // Допиши оптимальный алгоритм для фильтрации элементов с учетом их параметров или геометрии
            // Стоит ли делать все итерации (проверки) в одном цикле или лучше разделить на этапы?
            // Оптимально ли будет использоваться память при большом количестве элементов?

            //  Все результаты собери в StringBuilder:
            //  = targetElements.Count;
            //  = assignmentResults.Count(r => r.AssignedFloor != null);
            //  = floorModels;
            //  = true;

            return stringBuilder.ToString();
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
        /// Нативная фильтрация элементов с заданным параметром
        /// </summary>
        public IList<Element> GetFilteredElements(Document doc, SharedParameterElement parameter, ElementFilter elementFilter)
        {
            return new FilteredElementCollector(doc)
                .WhereHasSharedParameter(parameter)
                .WhereElementIsViewIndependent()
                .WhereElementIsNotElementType()
                .WherePasses(elementFilter)
                .ToElements();
        }

        /// <summary>
        /// Создает фильтр пересекающиеся с заданной 3D-границей и диапазоном высот.
        /// </summary>
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
        /// Выполняет полный цикл анализа и назначения элементов к этажам
        /// </summary>
        public string ExecuteFullAssignment(Guid targetParameterGuid)
        {
            StringBuilder stringBuilder = new();

            // Этап 1: Подготовка данных
            (_, List<FloorInfo> floorModels, Outline projectBoundary, SharedParameterElement parameter) = PrepareData(targetParameterGuid);

            // Этап 2: Получение всех целевых элементов один раз
            List<Element> candidateElements = GetAllCandidateElements(parameter);
            _ = stringBuilder.AppendLine($"Найдено кандидатов: {candidateElements.Count}");

            // Этап 3: Пакетная обработка элементов
            List<LevelAssignmentResult> assignmentResults = ProcessElementsInBatches(candidateElements, floorModels, projectBoundary);

            // Этап 4: Формирование статистики
            string stats = GenerateStatistics(assignmentResults, floorModels);
            _ = stringBuilder.Append(stats);

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Подготовка всех необходимых данных
        /// </summary>
        private (List<Level> levels, List<FloorInfo> floorModels, Outline boundary, SharedParameterElement parameter)
            PrepareData(Guid targetParameterGuid)
        {
            List<Level> levels = GetValidLevels(_document);
            List<FloorInfo> floorModels = _floorInfoGenerator.GenerateFloorModels(levels);
            Outline projectBoundary = _boundaryCalculator.ComputeProjectBoundary(_document, ref floorModels);
            SharedParameterElement parameter = SharedParameterElement.Lookup(_document, targetParameterGuid);

            return (levels, floorModels, projectBoundary, parameter);
        }

        /// <summary>
        /// Получение всех элементов-кандидатов за один запрос
        /// </summary>
        private List<Element> GetAllCandidateElements(SharedParameterElement parameter)
        {
            ElementMulticategoryFilter categoryFilter = new(CollectorHelper.GetModelCategoryIds(_document));

            return [.. new FilteredElementCollector(_document)
                .WhereHasSharedParameter(parameter)
                .WhereElementIsViewIndependent()
                .WhereElementIsNotElementType()
                .WherePasses(categoryFilter)];
        }

        /// <summary>
        /// Обработка элементов пакетами для оптимизации памяти
        /// </summary>
        private List<LevelAssignmentResult> ProcessElementsInBatches(List<Element> elements, List<FloorInfo> floorModels)
        {
            const int BATCH_SIZE = 1000; // Размер пакета для обработки
            List<LevelAssignmentResult> results = new(elements.Count);
            HashSet<ElementId> processedElements = new(elements.Count);

            for (int i = 0; i < elements.Count; i += BATCH_SIZE)
            {
                IEnumerable<Element> batch = elements.Skip(i).Take(BATCH_SIZE);
                List<LevelAssignmentResult> batchResults = ProcessBatch(batch, floorModels, processedElements);
                results.AddRange(batchResults);

                // Принудительная очистка памяти каждого пакета
                if (i % BATCH_SIZE == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            return results;
        }

        /// <summary>
        /// Обработка одного пакета элементов
        /// </summary>
        private List<LevelAssignmentResult> ProcessBatch(IEnumerable<Element> batch, List<FloorInfo> floorModels, HashSet<ElementId> processedElements)
        {
            List<LevelAssignmentResult> results = [];

            foreach (Element element in batch)
            {
                // Избегаем дублирования
                if (!processedElements.Add(element.Id))
                {
                    continue;
                }

                LevelAssignmentResult result = DetermineElementFloor(element, floorModels);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Определение этажа для элемента с использованием многоуровневой стратегии
        /// </summary>
        private LevelAssignmentResult DetermineElementFloor(Element element, List<FloorInfo> floorModels)
        {
            LevelAssignmentResult result = new(element);

            // Стратегия 1: Определение по параметрам уровня
            if (TryDetermineByLevelParameters(element, floorModels, result))
            {
                return result;
            }

            // Стратегия 2: Геометрический анализ
            if (TryDetermineByGeometry(element, floorModels, result))
            {
                return result;
            }

            // Стратегия 3: Пространственный анализ
            result.Method = Determination.Failed;
            result.Message = "Не удалось определить этаж!";
            return result;
        }

        /// <summary>
        /// Определение по параметрам уровня (самый быстрый метод)
        /// </summary>
        private bool TryDetermineByLevelParameters(Element element, List<FloorInfo> floorModels, LevelAssignmentResult result)
        {
            foreach (FloorInfo floor in floorModels)
            {
                HashSet<ElementId> levelIds = floor.ContainedLevels.Select(l => l.Id).ToHashSet();

                if (_levelDeterminator.IsOnLevel(element, ref levelIds))
                {
                    result.Method = Determination.ParameterBased;
                    result.AssignedFloor = floor;
                    result.Confidence = 0.9f;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Определение по геометрии элемента
        /// </summary>
        private bool TryDetermineByGeometry(Element element, List<FloorInfo> floorModels, LevelAssignmentResult result)
        {
            try
            {
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                if (bbox == null)
                {
                    return false;
                }

                ElementSpatialData elementData = new()
                {
                    Element = element,
                    BoundingBox = bbox,
                    Centroid = (bbox.Min + bbox.Max) / 2
                };

                List<FloorInfo> sortedFloors = floorModels.OrderBy(f => f.InternalElevation).ToList();

                if (_levelDeterminator.DetermineFloorByGeometry(elementData, ref sortedFloors))
                {
                    // Находим наиболее подходящий этаж
                    FloorInfo matchingFloor = FindBestMatchingFloor(elementData, floorModels);
                    if (matchingFloor != null)
                    {
                        result.IsSuccess = true;
                        result.Method = Determination.GeometricAnalysis;
                        result.Confidence = 0.7f;
                        result.AssignedFloor = matchingFloor;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = $"Ошибка геометрического анализа: {ex.Message}";
            }

            return false;
        }

        /// <summary>
        /// Поиск наиболее подходящего этажа для элемента
        /// </summary>
        private FloorInfo FindBestMatchingFloor(ElementSpatialData elementData, List<FloorInfo> floorModels)
        {
            return floorModels
                .Where(floor => _levelDeterminator.IsPointContained(elementData.Centroid, floor.BoundingBox))
                .OrderBy(floor => Math.Abs(floor.InternalElevation - elementData.Centroid.Z))
                .FirstOrDefault();
        }

        /// <summary>
        /// Формирование статистики результатов
        /// </summary>
        private string GenerateStatistics(List<LevelAssignmentResult> results, List<FloorInfo> floorModels)
        {
            StringBuilder stats = new();

            _ = stats.AppendLine($"Всего обработано элементов: {results.Count}");
            _ = stats.AppendLine($"Успешно назначено: {results.Count(r => r.IsSuccess)}");
            _ = stats.AppendLine($"Не удалось назначить: {results.Count(r => !r.IsSuccess)}");
            _ = stats.AppendLine($"Всего этажей: {floorModels.Count}");

            // Статистика по методам определения
            Dictionary<Determination, int> methodStats = results.Where(r => r.IsSuccess)
                .GroupBy(r => r.Method)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var (method, count) in methodStats)
            {
                _ = stats.AppendLine($"{method}: {count} элементов");
            }

            return stats.ToString();
        }

    }
}
