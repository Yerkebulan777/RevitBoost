using RevitUtils;

namespace LevelAssignment
{
    internal sealed class LevelService
    {
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }


        public List<Level> GetValidLevels(Document doc, double maxHeightInMeters = 100)
        {
            double maximum = UnitManager.MmToFoot(maxHeightInMeters * 1000);
            ParameterValueProvider provider = new(new ElementId(BuiltInParameter.LEVEL_ELEV));
            FilterDoubleRule rule = new(provider, new FilterNumericLess(), maximum, 5E-3);

            return [.. new FilteredElementCollector(doc).OfClass(typeof(Level))
                .WherePasses(new ElementParameterFilter(rule)).Cast<Level>()
                .OrderBy(x => x.Elevation).GroupBy(x => x.Elevation)
                .Select(x => x.First())];
        }


        public void CalculateBoundingPoints(Document doc, List<Level> levels, double minimum)
        {
            List<ElementId> modelCategoryIds = CollectorHelper.GetModelCategoryIds(doc);

            HashSet<ElementId> visibleElementIds = [];

            foreach (Level currentLevel in levels)
            {
                foreach (ViewPlan floorPlan in GetViewPlansByLevel(doc, currentLevel))
                {
                    FilteredElementCollector elements = GetInstancesInView(doc, floorPlan, modelCategoryIds);

                    foreach (Element element in elements.Where(el => visibleElementIds.Add(el.Id)))
                    {
                        BoundingBoxXYZ bbox = element.get_BoundingBox(null);

                        if ((bbox?.Enabled) is true)
                        {
                            XYZ minPoint = bbox.Min;
                            XYZ maxPoint = bbox.Max;

                            if (minPoint.DistanceTo(maxPoint) > minimum)
                            {
                                UpdateBoundingLimits(minPoint, maxPoint);
                            }
                        }
                    }
                }
            }
        }


        private void UpdateBoundingLimits(XYZ minPoint, XYZ maxPoint)
        {
            MinX = Math.Min(MinX, minPoint.X);
            MinY = Math.Min(MinY, minPoint.Y);
            MaxX = Math.Max(MaxX, maxPoint.X);
            MaxY = Math.Max(MaxY, maxPoint.Y);
        }

        /// <summary>
        /// Получает все планы этажей для указанного уровня
        /// </summary>
        private static List<ViewPlan> GetViewPlansByLevel(Document doc, Level level)
        {
            return [.. new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan)).OfType<ViewPlan>()
                .Where(pln => !pln.IsTemplate && pln.IsValidObject && pln.GenLevel.Id == level.Id)];
        }

        /// <summary>
        /// Получает видимые элементы в указанном виде используя FilteredElementCollector
        /// </summary>
        private static FilteredElementCollector GetInstancesInView(Document doc, View view, List<ElementId> categoryIds)
        {
            return new FilteredElementCollector(doc, view.Id)
                .WherePasses(new ElementMulticategoryFilter(categoryIds))
                .WhereElementIsViewIndependent()
                .WhereElementIsNotElementType();
        }



        public LogicalOrFilter CreateIntersectBoxFilter(ref Level level, int floorNumber, List<Level> levels, bool visible = false)
        {
            double clearance = UnitManager.MmToFoot(100);

            List<Level> sortedLevels = [.. levels.OrderBy(x => x.Elevation)];

            double height = GetLevelHeight(level, floorNumber, sortedLevels, out double elevation);

            XYZ minPoint = Transform.Identity.OfPoint(new XYZ(MinX, MinY, elevation + clearance));

            XYZ maxPoint = Transform.Identity.OfPoint(new XYZ(MaxX, MaxY, elevation + height));

            Solid solid = SolidHelper.CreateSolidBoxByPoint(minPoint, maxPoint, height);

            if (visible)
            {
                solid.CreateDirectShape(level.Document);
            }

            Outline outline = new(minPoint, maxPoint);

            ElementIntersectsSolidFilter solidFilter = new(solid);

            BoundingBoxIsInsideFilter insideBoxFilter = new(outline: outline);

            BoundingBoxIntersectsFilter intersectBoxFilter = new(outline: outline);

            LogicalOrFilter logicBoundingOrFilter = new(insideBoxFilter, intersectBoxFilter);

            LogicalOrFilter logicIntersectOrFilter = new(logicBoundingOrFilter, solidFilter);

            return logicIntersectOrFilter;

        }


        public static double GetLevelHeight(Level level, int floorNumber, List<Level> sortedLevels, out double evelation)
        {
            double result = 0;

            evelation = level.Elevation;

            Level aboveLevel = sortedLevels.FirstOrDefault(x => x.Elevation > level.Elevation);
            Level belowLevel = sortedLevels.LastOrDefault(x => x.Elevation < level.Elevation);

            if (floorNumber > 0 && aboveLevel is not null && belowLevel is not null)
            {
                result = Math.Abs(aboveLevel.Elevation - level.Elevation);
            }
            else if (floorNumber != 0 && aboveLevel is null)
            {
                result = Math.Abs(level.Elevation - belowLevel.Elevation);
            }
            else if (floorNumber < 0 && belowLevel is null)
            {
                result = Math.Abs(aboveLevel.Elevation - level.Elevation);
                double subtract = UnitManager.MmToFoot(3000);
                evelation -= subtract;
                result += subtract;
            }

            return result;
        }


    }
}
