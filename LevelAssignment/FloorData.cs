using Autodesk.Revit.DB;
using RevitUtils;
using System.Diagnostics;

namespace LevelAssignment
{
    public sealed class FloorData : IDisposable
    {
        public SharedParameterElement LevelSharedParameter { get; internal set; }
        public List<ElementId> ContainedLevelIds { get; internal set; }
        public ElementFilter BoundingRegionFilter { get; internal set; }
        public ElementFilter ModelCategoryFilter { get; internal set; }
        public ElementFilter CombinedLevelFilter { get; internal set; }
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
            FilterNumericEquals evaluator = new();
            IList<ElementFilter> allLevelFilters = [];

            foreach (ElementId levelId in ContainedLevelIds)
            {
                allLevelFilters.Add(new ElementLevelFilter(levelId));
                allLevelFilters.Add(CollectorHelper.BuildNumericParameterFilter(BuiltInParameter.LEVEL_PARAM, evaluator, levelId));
                allLevelFilters.Add(CollectorHelper.BuildNumericParameterFilter(BuiltInParameter.FAMILY_LEVEL_PARAM, evaluator, levelId));
                allLevelFilters.Add(CollectorHelper.BuildNumericParameterFilter(BuiltInParameter.SCHEDULE_LEVEL_PARAM, evaluator, levelId));
            }

            CombinedLevelFilter = new LogicalOrFilter(allLevelFilters);
        }

        /// <summary>
        /// Создает фильтр пересекающиеся с заданной 3D-границей и диапазоном высот.
        /// </summary>
        public void CreateIntersectFilter(Outline boundary, double offset, double clearance)
        {
            double height = Height - clearance;
            double elevation = BaseElevation - offset;

            if (height > offset)
            {
                XYZ minPoint = boundary.MinimumPoint;
                XYZ maxPoint = boundary.MaximumPoint;

                minPoint = Transform.Identity.OfPoint(new XYZ(minPoint.X, minPoint.Y, elevation));
                maxPoint = Transform.Identity.OfPoint(new XYZ(maxPoint.X, maxPoint.Y, elevation + height));

                FloorBoundingSolid = SolidHelper.CreateSolidBoxByPoint(minPoint, maxPoint);

                GeometryOutline = new Outline(minPoint, maxPoint);

                if (FloorBoundingSolid is null)
                {
                    throw new InvalidOperationException($"Failed to create bounding solid!");
                }

                BoundingBoxIntersectsFilter boundingBoxFilter = new(GeometryOutline);
                ElementIntersectsSolidFilter solidIntersectionFilter = new(FloorBoundingSolid);

                BoundingRegionFilter = new LogicalAndFilter(boundingBoxFilter, solidIntersectionFilter);

                return;
            }

            throw new InvalidOperationException($"Height must be greater! {DisplayName}");
        }

        /// <summary>
        /// Проверяет наличие элементов в документе, соответствующих заданным фильтрам
        /// </summary>
        public string AssertElementsExistence(Document document)
        {
            Debug.Assert(CombinedLevelFilter is not null, "CombinedLevelFilter is not initialized!");
            Debug.Assert(ModelCategoryFilter is not null, "ModelCategoryFilter is not initialized!");
            Debug.Assert(BoundingRegionFilter is not null, "BoundingRegionFilter is not initialized!");

            ElementParameterFilter valueFilter = new(new HasValueFilterRule(LevelSharedParameter.Id));

            ElementParameterFilter paramFilter = new(new SharedParameterApplicableRule(LevelSharedParameter.Name));

            ElementFilter[] elementFilters = [ModelCategoryFilter, BoundingRegionFilter, CombinedLevelFilter, paramFilter, valueFilter];

            (_, string output) = CollectorHelper.GetFilteredElementCollector(document, elementFilters);

            return output;
        }

        /// <summary>
        /// Создает фильтр для элементов на заданных уровнях
        /// </summary>
        public FilteredElementCollector CreateLevelCollector(Document doc)
        {
            string paramName = LevelSharedParameter.Name;

            return new FilteredElementCollector(doc)
                .WherePasses(BoundingRegionFilter)
                .WherePasses(ModelCategoryFilter)
                .WherePasses(CombinedLevelFilter)
                .WhereHasParameter(paramName);
        }

        /// <summary>
        /// Создает фильтр исключая заданные элементы
        /// </summary>
        public FilteredElementCollector CreateExcludedCollector(Document doc, ICollection<ElementId> elementIds)
        {
            string paramName = LevelSharedParameter.Name;

            return new FilteredElementCollector(doc)
                .WherePasses(BoundingRegionFilter)
                .WherePasses(ModelCategoryFilter)
                .WhereHasParameter(paramName)
                .ExcludeElements(elementIds);
        }

        /// <summary>
        /// Определяет этаж на основе геометрического анализа высоты элемента
        /// </summary>
        public bool IsContained(in Element element)
        {
            BoundingBoxXYZ bbox = element?.get_BoundingBox(null);

            if (bbox is null || !bbox.Enabled)
            {
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
                    CombinedLevelFilter?.Dispose();
                    ElementExclusionFilter?.Dispose();
                    BoundingRegionFilter?.Dispose();
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
