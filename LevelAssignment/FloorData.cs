using Autodesk.Revit.DB;
using RevitUtils;
using System.Diagnostics;

namespace LevelAssignment
{
    public sealed class FloorData : IDisposable
    {
        public List<ElementId> ContainedLevelIds { get; internal set; }
        public SharedParameterElement LevelSharedParameter { get; internal set; }
        public ElementFilter SpatialIntersectionFilter { get; internal set; }
        public ElementFilter ModelCategoryFilter { get; internal set; }
        public ElementFilter AggregatedLevelFilter { get; internal set; }
        public ElementFilter ElementExclusionFilter { get; internal set; }
        public BoundingBoxXYZ BoundingBox { get; internal set; }
        public Solid FloorBoundingSolid { get; internal set; }
        public Outline GeometryOutline { get; internal set; }
        public double BaseElevation { get; internal set; }
        public double ProjectElevation { get; internal set; }
        public string DisplayName { get; internal set; }
        public double Height { get; internal set; }
        public int FloorIndex { get; internal set; }

        private bool _disposed;

        public FloorData(int floorNumber, List<Level> sortedLevels)
        {
            ContainedLevelIds = [.. sortedLevels.Select(l => l.Id)];
            Level baseLevel = sortedLevels.FirstOrDefault();
            ProjectElevation = baseLevel.ProjectElevation;
            BaseElevation = baseLevel.Elevation;
            DisplayName = baseLevel.Name;
            FloorIndex = floorNumber;
        }

        /// <summary>
        /// Создает комплексный фильтр для поиска элементов по нескольким уровням
        /// используя все доступные параметры уровней
        /// </summary>
        public void AggregateLevelFilter()
        {
            List<ElementFilter> allLevelFilters = [];

            foreach (ElementId levelId in ContainedLevelIds)
            {
                List<ElementFilter> singleLevelFilters =
                [
                    new ElementLevelFilter(levelId),
                    CreateParameterFilter(BuiltInParameter.LEVEL_PARAM, levelId),
                    CreateParameterFilter(BuiltInParameter.FAMILY_LEVEL_PARAM, levelId),
                    CreateParameterFilter(BuiltInParameter.SCHEDULE_LEVEL_PARAM, levelId)
                ];

                allLevelFilters.AddRange(singleLevelFilters);
            }

            AggregatedLevelFilter = new LogicalOrFilter(allLevelFilters);
        }

        /// <summary>
        /// Создает фильтр пересекающиеся с заданной 3D-границей и диапазоном высот.
        /// </summary>
        public void CreateIntersectFilter(Outline boundary, double offset, double clearance)
        {
            double adjustedHeight = Height - clearance;

            if (adjustedHeight > offset)
            {
                XYZ minPoint = boundary.MinimumPoint;
                XYZ maxPoint = boundary.MaximumPoint;

                double elevation = BaseElevation - offset;

                minPoint = Transform.Identity.OfPoint(new XYZ(minPoint.X, minPoint.Y, elevation));
                maxPoint = Transform.Identity.OfPoint(new XYZ(maxPoint.X, maxPoint.Y, elevation + adjustedHeight));

                FloorBoundingSolid = SolidHelper.CreateSolidBoxByPoint(minPoint, maxPoint, adjustedHeight);

                GeometryOutline = new Outline(minPoint, maxPoint);

                if (FloorBoundingSolid is null)
                {
                    throw new InvalidOperationException($"Failed to create bounding solid!");
                }

                BoundingBoxIntersectsFilter boundingBoxFilter = new(GeometryOutline);
                ElementIntersectsSolidFilter solidIntersectionFilter = new(FloorBoundingSolid);

                SpatialIntersectionFilter = new LogicalOrFilter(boundingBoxFilter, solidIntersectionFilter);

                return;
            }

            throw new InvalidOperationException($"Height must be greater! {DisplayName}");
        }

        /// <summary>
        /// Создает фильтр для элементов на заданных уровнях
        /// </summary>
        public FilteredElementCollector CreateLevelFilteredCollector(Document doc)
        {
            string paramName = LevelSharedParameter.Name;
            return new FilteredElementCollector(doc)
                .WherePasses(ModelCategoryFilter)
                .WherePasses(AggregatedLevelFilter)
                .WherePasses(SpatialIntersectionFilter)
                .WhereSharedParameterApplicable(paramName);
        }

        /// <summary>
        /// Создает фильтр исключая заданные элементы
        /// </summary>
        public FilteredElementCollector CreateExcludedCollector(Document doc, ICollection<ElementId> elementIds)
        {
            FilteredElementCollector collector;
            string paramName = LevelSharedParameter.Name;
            collector = new FilteredElementCollector(doc)
                        .WherePasses(ModelCategoryFilter)
                        .WherePasses(SpatialIntersectionFilter)
                        .WhereSharedParameterApplicable(paramName);

            return collector.ExcludeElements(elementIds);
        }

        /// <summary>
        /// Создает фильтр по конкретному параметру уровня
        /// </summary>
        private static ElementFilter CreateParameterFilter(BuiltInParameter levelBuiltInParam, ElementId levelId)
        {
            FilterNumericEquals evalutor = new();
            ParameterValueProvider valueProvider = new(new ElementId(levelBuiltInParam));
            FilterElementIdRule filterRule = new(valueProvider, evalutor, levelId);

            return new ElementParameterFilter(filterRule);
        }

        /// <summary>
        /// Определяет этаж на основе геометрического анализа высоты элемента
        /// </summary>
        public bool IsContained(in Element element)
        {
            BoundingBoxXYZ bbox = element?.get_BoundingBox(null);

            if (bbox is null || !bbox.Enabled)
            {
                Debug.Fail("BoundingBox not enabled!");
                return false;
            }

            XYZ center = (bbox.Min + bbox.Max) * 0.5;

            return GeometryOutline.Contains(center, double.Epsilon);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    ContainedLevelIds?.Clear();
                    ModelCategoryFilter?.Dispose();
                    LevelSharedParameter?.Dispose();
                    AggregatedLevelFilter?.Dispose();
                    ElementExclusionFilter?.Dispose();
                    SpatialIntersectionFilter?.Dispose();
                    FloorBoundingSolid?.Dispose();
                    GeometryOutline?.Dispose();
                    BoundingBox?.Dispose();
                }
                // Free unmanaged resources
                _disposed = true;
            }
        }

        ~FloorData()
        {
            Dispose(false);
        }
    }
}
