using RevitUtils;

namespace LevelAssignment
{
    internal sealed class LevelService
    {
        private readonly double minX;
        private readonly double maxX;
        private readonly double minY;
        private readonly double maxY;


        private readonly List<ElementId> categoryIds;


        public LevelService(Document doc)
        {
            categoryIds = CollectorHelper.GetModelCategoryIds(doc);
        }


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


        public static void GetBoundingPoints(Document doс, in List<Level> models, out double minX, out double maxX, out double minY, out double maxY)
        {
            minX = double.PositiveInfinity;
            maxX = double.NegativeInfinity;
            minY = double.PositiveInfinity;
            maxY = double.NegativeInfinity;


            foreach (Level level in models)
            {
                if (level != null)
                {

                    FilteredElementCollector collector = GetGeometryByLevel(doс, level, categoryIds);

                    double tolerance = UnitManager.MmToFoot(9000);
                    double adding = UnitManager.MmToFoot(3000);

                    foreach (Element element in collector)
                    {
                        BoundingBoxXYZ bbox = element.get_BoundingBox(null);

                        if (bbox != null && bbox.Enabled)
                        {
                            XYZ pointMin = bbox.Min;
                            XYZ pointMax = bbox.Max;

                            if (pointMin.DistanceTo(pointMax) < tolerance)
                            {
                                minX = Math.Min(minX, pointMin.X - adding);
                                minY = Math.Min(minY, pointMin.Y - adding);
                                maxX = Math.Max(maxX, pointMax.X + adding);
                                maxY = Math.Max(maxY, pointMax.Y + adding);
                            }
                        }
                    }
                }
                else
                {
                    throw new ArgumentNullException("Not found level");
                }
            }
        }


        public FilteredElementCollector GetGeometryByLevel(Document doc, Level level, List<ElementId> catIds)
        {
            return new FilteredElementCollector(doc)
                .WherePasses(new ElementLevelFilter(level.Id))
                .WherePasses(new ElementMulticategoryFilter(catIds))
                .WhereElementIsViewIndependent()
                .WhereElementIsNotElementType();
        }


        private LogicalOrFilter CreateIntersectBoxFilter(ref Level model, int floorNumber, List<Level> levels, bool visible = true)
        {
            double height = GetLevelHeight(model, floorNumber, levels, out double elevation);

            XYZ minPoint = Transform.Identity.OfPoint(new(minX, minY, elevation));

            XYZ maxPoint = Transform.Identity.OfPoint(new(maxX, maxY, elevation + height));

            Solid solid = SolidHelper.CreateSolidBoxByPoint(minPoint, maxPoint, height);
            _ = new
            BoundingBoxXYZ()
            {
                Min = minPoint,
                Max = maxPoint,
                Enabled = true,
            };

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
