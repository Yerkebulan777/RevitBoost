using RevitUtils;

namespace LevelAssignment
{
    public sealed class FloorInfo
    {
        public List<ElementId> ContainedLevelIds { get; internal set; }
        public SharedParameterElement LevelSharedParameter { get; internal set; }
        public ElementFilter GeometryIntersectionFilter { get; internal set; }
        public ElementFilter ModelCategoryFilter { get; internal set; }
        public ElementFilter LevelFilter { get; internal set; }
        public BoundingBoxXYZ BoundingBox { get; internal set; }
        public double InternalElevation { get; private set; }
        public double ProjectElevation { get; private set; }
        public string DisplayName { get; private set; }
        public double Height { get; internal set; }
        public int Index { get; private set; }


        public FloorInfo(int floorNumber, IEnumerable<Level> sortedLevels)
        {
            ContainedLevelIds = [.. sortedLevels.Select(l => l.Id)];
            Level baseLevel = sortedLevels.FirstOrDefault();
            ProjectElevation = baseLevel.ProjectElevation;
            InternalElevation = baseLevel.Elevation;
            DisplayName = baseLevel.Name;
            Index = floorNumber;

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

            LevelFilter = new LogicalOrFilter(allLevelFilters);
        }

        /// <summary>
        /// Создает фильтр пересекающиеся с заданной 3D-границей и диапазоном высот.
        /// </summary>
        public void CreateIntersectFilter(Outline boundary, double offset, double clearance)
        {
            double height = Height;

            XYZ minPoint = boundary.MinimumPoint;
            XYZ maxPoint = boundary.MaximumPoint;

            double elevation = InternalElevation;

            minPoint = Transform.Identity.OfPoint(new XYZ(minPoint.X, minPoint.Y, elevation + clearance - offset));

            maxPoint = Transform.Identity.OfPoint(new XYZ(maxPoint.X, maxPoint.Y, elevation + height - offset));

            Solid floorSolid = SolidHelper.CreateSolidBoxByPoint(minPoint, maxPoint, height);

            Outline outline = new(minPoint, maxPoint);

            ElementIntersectsSolidFilter solidFilter = new(floorSolid);
            BoundingBoxIntersectsFilter boundingBoxFilter = new(outline);

            GeometryIntersectionFilter = new LogicalOrFilter(boundingBoxFilter, solidFilter);
        }

        /// <summary>
        /// Получение всех элементов за один запрос
        /// </summary>
        public FilteredElementCollector CreateFilteredElementCollector(Document doc)
        {
            string sharedParameterName = LevelSharedParameter?.Name;

            return new FilteredElementCollector(doc)
                        .WherePasses(LevelFilter)
                        .WherePasses(ModelCategoryFilter)
                        .WherePasses(GeometryIntersectionFilter)
                        .WhereSharedParameterApplicable(sharedParameterName);
        }

        /// <summary>
        /// Создает фильтр по конкретному параметру уровня
        /// </summary>
        private ElementFilter CreateParameterFilter(BuiltInParameter levelBuiltInParam, ElementId levelId)
        {
            FilterNumericEquals evalutor = new();
            ParameterValueProvider valueProvider = new(new ElementId(levelBuiltInParam));
            FilterElementIdRule filterRule = new(valueProvider, evalutor, levelId);

            return new ElementParameterFilter(filterRule);
        }



    }
}
