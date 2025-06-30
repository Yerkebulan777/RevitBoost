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



    }
}
