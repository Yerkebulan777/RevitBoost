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


        public void CalculateBoundingPoints(Document doc, List<Level> levels)
        {
            List<ElementId> categoryIds = CollectorHelper.GetModelCategoryIds(doc);

            double tolerance = UnitManager.MmToFoot(500);

            foreach (Level level in levels)
            {
                FilteredElementCollector collector = GetInstancesyByLevel(doc, level, categoryIds);

                foreach (Element element in collector)
                {
                    BoundingBoxXYZ bbox = element.get_BoundingBox(null);

                    if (bbox?.Enabled == true)
                    {
                        XYZ pntMin = bbox.Min;
                        XYZ pntMax = bbox.Max;

                        // Проверяем размер элемента
                        if (pntMin.DistanceTo(pntMax) < tolerance)
                        {
                            MinX = Math.Min(MinX, pntMin.X);
                            MinY = Math.Min(MinY, pntMin.Y);
                            MaxX = Math.Max(MaxX, pntMax.X);
                            MaxY = Math.Max(MaxY, pntMax.Y);
                        }
                    }
                }
            }
        }


        public FilteredElementCollector GetInstancesyByLevel(Document doc, Level level, List<ElementId> catIds)
        {
            return new FilteredElementCollector(doc)
                .WherePasses(new ElementLevelFilter(level.Id))
                .WherePasses(new ElementMulticategoryFilter(catIds))
                .WhereElementIsViewIndependent()
                .WhereElementIsNotElementType();
        }


        public LogicalOrFilter CreateIntersectBoxFilter(ref Level model, int floorNumber, List<Level> levels, bool visible = true)
        {
            double height = GetLevelHeight(model, floorNumber, levels, out double elevation);

            XYZ minPoint = Transform.Identity.OfPoint(new XYZ(MinX, MinY, elevation));

            XYZ maxPoint = Transform.Identity.OfPoint(new XYZ(MaxX, MaxY, elevation + height));

            Solid solid = SolidHelper.CreateSolidBoxByPoint(minPoint, maxPoint, height);

            if (visible)
            {
                solid.CreateDirectShape(model.Document);
            }

            Outline outline = new(minPoint, maxPoint);

            ElementIntersectsSolidFilter solidFilter = new(solid);

            BoundingBoxIsInsideFilter insideBoxFilter = new(outline: outline);

            BoundingBoxIntersectsFilter intersectBoxFilter = new(outline: outline);

            LogicalOrFilter logicBoundingOrFilter = new(insideBoxFilter, intersectBoxFilter);

            LogicalOrFilter logicIntersectOrFilter = new(logicBoundingOrFilter, solidFilter);

            return logicIntersectOrFilter;

        }


        public static double GetLevelHeight(Level level, int floorNumber, List<Level> levels, out double evelation)
        {
            double result = 0;

            evelation = level.Elevation;
            double clearance = UnitManager.MmToFoot(150);

            Level abovetLevel = levels.FirstOrDefault(x => x.ProjectElevation > level.ProjectElevation);
            Level belowLevel = levels.LastOrDefault(x => x.ProjectElevation < level.ProjectElevation);

            if (floorNumber > 0 && abovetLevel is not null && belowLevel is not null)
            {
                result = Math.Abs(abovetLevel.ProjectElevation - level.ProjectElevation - clearance);
            }
            else if (floorNumber != 0 && abovetLevel is null)
            {
                result = Math.Abs(level.ProjectElevation - belowLevel.ProjectElevation - clearance);
            }
            else if (floorNumber < 0 && belowLevel is null)
            {
                result = Math.Abs(abovetLevel.ProjectElevation - level.ProjectElevation);
                double subtract = UnitManager.MmToFoot(3000);
                evelation -= subtract;
                result += subtract;
            }

            return result;
        }


    }
}
