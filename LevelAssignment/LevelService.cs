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
                .OrderBy(x => x.Elevation)];
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



        public LogicalOrFilter CreateIntersectBoxFilter(Document doc, FloorModel level, int floorNumber, List<FloorModel> floorModels, bool visible = false)
        {
            double clearance = UnitManager.MmToFoot(100);

            double height = GetLevelHeight(level, floorNumber, floorModels, out double elevation);

            XYZ minPoint = Transform.Identity.OfPoint(new XYZ(MinX, MinY, elevation + clearance));

            XYZ maxPoint = Transform.Identity.OfPoint(new XYZ(MaxX, MaxY, elevation + height));

            Solid solid = SolidHelper.CreateSolidBoxByPoint(minPoint, maxPoint, height);

            if (visible)
            {
                solid.CreateDirectShape(doc);
            }

            Outline outline = new(minPoint, maxPoint);

            ElementIntersectsSolidFilter solidFilter = new(solid);

            BoundingBoxIsInsideFilter insideBoxFilter = new(outline: outline);

            BoundingBoxIntersectsFilter intersectBoxFilter = new(outline: outline);

            LogicalOrFilter logicBoundingOrFilter = new(insideBoxFilter, intersectBoxFilter);

            LogicalOrFilter logicIntersectOrFilter = new(logicBoundingOrFilter, solidFilter);

            return logicIntersectOrFilter;

        }


        public static double GetLevelHeight(FloorModel current, int floorNumber, List<FloorModel> floors, out double elevation)
        {
            double result = 0;

            elevation = current.ProjectElevation;

            List<FloorModel> sortedFloors = [.. floors.OrderBy(x => x.ProjectElevation)];
            FloorModel aboveFloor = sortedFloors.FirstOrDefault(x => x.ProjectElevation > current.ProjectElevation);
            FloorModel belowFloor = sortedFloors.LastOrDefault(x => x.ProjectElevation < current.ProjectElevation);

            if (floorNumber > 0 && aboveFloor is not null && belowFloor is not null)
            {
                result = Math.Abs(aboveFloor.ProjectElevation - current.ProjectElevation);
            }
            else if (floorNumber > 1 && aboveFloor is null)
            {
                result = Math.Abs(current.ProjectElevation - belowFloor.ProjectElevation);
            }
            else if (floorNumber < 0 && belowFloor is null)
            {
                result = Math.Abs(aboveFloor.ProjectElevation - current.ProjectElevation);
                double subtract = UnitManager.MmToFoot(3000);
                elevation -= subtract;
                result += subtract;
            }

            return result;
        }


    }
}
