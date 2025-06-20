using RevitUtils;
using System.Diagnostics;

namespace LevelAssignment
{
    internal sealed class LevelService
    {
        /// <summary>
        /// Представляет границы прямоугольной области в 2D пространстве
        /// </summary>
        public sealed class BoundarySize
        {
            public double MinX { get; set; }

            public double MaxX { get; set; }

            public double MinY { get; set; }

            public double MaxY { get; set; }

            // Вычисляемые свойства для удобства
            public double Width => MaxX - MinX;
            public double Height => MaxY - MinY;
            public double Area => Width * Height;


            // Фабричный метод для создания валидных границ
            public static BoundarySize Create(double minX, double maxX, double minY, double maxY)
            {
                if (minX > maxX || minY > maxY)
                {
                    Debug.WriteLine("Ошибка: Минимальные значения не могут быть больше максимальных");
                    throw new ArgumentException("Минимальные значения не могут быть больше максимальных");
                }

                return new BoundarySize
                {
                    MinX = minX,
                    MaxX = maxX,
                    MinY = minY,
                    MaxY = maxY
                };
            }
        }


        public enum BoundaryType
        {
            MinX,
            MaxX,
            MinY,
            MaxY
        }

        private readonly List<ElementId> categoryIds;
        private readonly Dictionary<BoundaryType, double> boundaries;
        
        public LevelService(Document doc)
        {
            categoryIds = CollectorHelper.GetModelCategoryIds(doc);

            boundaries = new Dictionary<BoundaryType, double>
            {
                [BoundaryType.MinX] = double.PositiveInfinity,
                [BoundaryType.MaxX] = double.NegativeInfinity,
                [BoundaryType.MinY] = double.PositiveInfinity,
                [BoundaryType.MaxY] = double.NegativeInfinity
            };
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


        public void CalculateBoundingPoints(Document doc, List<Level> levels)
        {
            boundaries[BoundaryType.MinX] = 0;
            boundaries[BoundaryType.MaxX] = 0;
            boundaries[BoundaryType.MinY] = 0;
            boundaries[BoundaryType.MaxY] = 0;

            foreach (Level level in levels.Where(l => l is not null))
            {
                if (level is not null)
                {
                    FilteredElementCollector collector = GetGeometryByLevel(doc, level, categoryIds);

                    double tolerance = UnitManager.MmToFoot(9000);
                    double padding = UnitManager.MmToFoot(3000);

                    foreach (Element element in collector)
                    {
                        BoundingBoxXYZ bbox = element.get_BoundingBox(null);

                        if (bbox?.Enabled == true)
                        {
                            XYZ pointMin = bbox.Min;
                            XYZ pointMax = bbox.Max;

                            // Проверяем размер элемента
                            if (pointMin.DistanceTo(pointMax) < tolerance)
                            {
                                // Обновляем границы используя enum
                                UpdateBoundary(BoundaryType.MinX, pointMin.X - padding, Math.Min);
                                UpdateBoundary(BoundaryType.MinY, pointMin.Y - padding, Math.Min);
                                UpdateBoundary(BoundaryType.MaxX, pointMax.X + padding, Math.Max);
                                UpdateBoundary(BoundaryType.MaxY, pointMax.Y + padding, Math.Max);
                            }
                        }
                    }
                }
            }
        }


        private void UpdateBoundary(BoundaryType type, double newValue, Func<double, double, double> comparer)
        {
            boundaries[type] = comparer(boundaries[type], newValue);
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
