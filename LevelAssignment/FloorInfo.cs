﻿using Autodesk.Revit.DB;
using RevitUtils;
using System.Diagnostics;

namespace LevelAssignment
{
    public sealed class FloorInfo
    {
        public List<ElementId> ContainedLevelIds { get; internal set; }
        public SharedParameterElement LevelSharedParameter { get; internal set; }
        public ElementFilter GeometryIntersectionFilter { get; internal set; }
        public ElementFilter ModelCategoryFilter { get; internal set; }
        public ElementFilter AggregatedLevelFilter { get; internal set; }
        public ElementFilter ElementExclusionFilter { get; internal set; }
        public Solid FloorBoundingSolid { get; internal set; }
        public Outline GeometryOutline { get; internal set; }
        public BoundingBoxXYZ BoundingBox { get; internal set; }
        public double InternalElevation { get; private set; }
        public double ProjectElevation { get; private set; }
        public string DisplayName { get; private set; }
        public double Height { get; internal set; }
        public int Index { get; private set; }


        public FloorInfo(int floorNumber, List<Level> sortedLevels)
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

            AggregatedLevelFilter = new LogicalOrFilter(allLevelFilters);
        }

        /// <summary>
        /// Создает фильтр пересекающиеся с заданной 3D-границей и диапазоном высот.
        /// </summary>
        public void CreateIntersectFilter(Outline boundary, double offset, double clearance)
        {
            if (Height > 0)
            {
                double height = Height;

                XYZ minPoint = boundary.MinimumPoint;
                XYZ maxPoint = boundary.MaximumPoint;

                double elevation = InternalElevation;

                minPoint = Transform.Identity.OfPoint(new XYZ(minPoint.X, minPoint.Y, elevation + clearance - offset));
                maxPoint = Transform.Identity.OfPoint(new XYZ(maxPoint.X, maxPoint.Y, elevation + height - offset));

                FloorBoundingSolid = SolidHelper.CreateSolidBoxByPoint(minPoint, maxPoint, height);
                GeometryOutline = new Outline(minPoint, maxPoint);

                BoundingBoxIntersectsFilter boundingBoxFilter = new(GeometryOutline);
                ElementIntersectsSolidFilter solidFilter = new(FloorBoundingSolid);

                GeometryIntersectionFilter = new LogicalOrFilter(boundingBoxFilter, solidFilter);

                return;
            }

            throw new InvalidOperationException("Height must be greater than zero!");
        }

        /// <summary>
        /// Создает фильтр для элементов на заданных уровнях
        /// </summary>
        public FilteredElementCollector CreateLevelFilteredCollector(Document doc)
        {
            return new FilteredElementCollector(doc)
                    .WherePasses(ModelCategoryFilter)
                    .WherePasses(AggregatedLevelFilter)
                    .WherePasses(GeometryIntersectionFilter)
                    .WhereSharedParameterApplicable(LevelSharedParameter.Name);
        }

        /// <summary>
        /// Создает фильтр исключая заданные элементы
        /// </summary>
        public FilteredElementCollector CreateExcludedCollector(Document doc, ICollection<ElementId> elementIds)
        {
            ElementExclusionFilter = new ExclusionFilter(elementIds);
            string paramName = LevelSharedParameter.Name;
            return new FilteredElementCollector(doc)
                    .WherePasses(ModelCategoryFilter)
                    .WherePasses(ElementExclusionFilter)
                    .WherePasses(GeometryIntersectionFilter)
                    .WhereSharedParameterApplicable(paramName);
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



    }
}
