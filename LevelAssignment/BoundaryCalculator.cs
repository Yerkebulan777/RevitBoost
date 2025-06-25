using RevitUtils;

namespace LevelAssignment
{
    internal sealed class BoundaryCalculator
    {
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }


        /// <summary>
        /// Получение границ видов планов этажей для всех уровней
        /// </summary>
        public void CalculateBoundingPoints(Document doc, List<Level> levels)
        {
            List<Outline> prioritizedOutlines = [];

            foreach (Level level in levels)
            {
                List<ViewPlan> floorPlans = GetViewPlansByLevel(doc, level);

                foreach (ViewPlan floorPlan in floorPlans)
                {
                    Outline viewBoundary = ExtractViewPlanBoundary(floorPlan, level);

                    if (viewBoundary != null)
                    {
                        prioritizedOutlines.Add(viewBoundary);
                    }
                }
            }

            ProcessBoundaries(prioritizedOutlines);
        }

        /// <summary>
        /// Получает все планы этажей для указанного уровня
        /// </summary>
        internal List<ViewPlan> GetViewPlansByLevel(Document doc, Level level)
        {
            return [.. new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan)).OfType<ViewPlan>()
                .Where(pln => !pln.IsTemplate && pln.GenLevel.Id == level.Id)];
        }

        /// <summary>
        /// Получение границ плана
        /// </summary>
        internal Outline ExtractViewPlanBoundary(ViewPlan floorPlan, Level level)
        {
            // Приоритет 1: Активный CropBox
            if (floorPlan.CropBoxActive && floorPlan.CropBox != null)
            {
                return TransformCropBox(floorPlan.CropBox, level);
            }

            // Приоритет 2: Свойство Outline вида (если доступно)
            BoundingBoxUV viewOutline = floorPlan.Outline;
            if (viewOutline is not null)
            {
                return TransformViewOutline(viewOutline, level);
            }

            // Приоритет 3: Анализ через CropRegionShapeManager
            ViewCropRegionShapeManager cropManager = floorPlan.GetCropRegionShapeManager();

            if (cropManager.CanHaveShape)
            {
                IList<CurveLoop> cropShapes = cropManager.GetCropShape();

                if (cropShapes.Any())
                {
                    return GetCropRegionOutline(cropShapes, level);
                }
            }

            return null;
        }

        /// <summary>
        /// Обработка границ
        /// </summary>
        private void ProcessBoundaries(List<Outline> prioritizedOutlines)
        {
            // Объединение всех границ
            foreach (Outline outline in prioritizedOutlines)
            {
                MinX = Math.Min(MinX, outline.MinimumPoint.X);
                MinY = Math.Min(MinY, outline.MinimumPoint.Y);
                MaxX = Math.Max(MaxX, outline.MaximumPoint.X);
                MaxY = Math.Max(MaxY, outline.MaximumPoint.Y);
            }
        }

        /// <summary>
        /// Преобразование outline вида в проектные координаты
        /// </summary>
        private Outline TransformViewOutline(BoundingBoxUV viewOutline, Level level)
        {
            BasePoint basePoint = GetProjectBasePoint(level.Document);

            XYZ offset = basePoint?.Position ?? XYZ.Zero;

            XYZ minPoint = new(
                viewOutline.Min.U - offset.X,
                viewOutline.Min.V - offset.Y,
                level.Elevation);

            XYZ maxPoint = new(
                viewOutline.Max.U - offset.X,
                viewOutline.Max.V - offset.Y,
                level.Elevation);

            return new Outline(minPoint, maxPoint);
        }

        /// <summary>
        /// Преобразует границы CropBox в проектные координаты
        /// </summary>
        private Outline TransformCropBox(BoundingBoxXYZ cropBox, Level level)
        {
            BasePoint basePoint = GetProjectBasePoint(level.Document);

            XYZ offset = basePoint?.Position ?? XYZ.Zero;

            XYZ minProjectPoint = new(
                cropBox.Min.X - offset.X,
                cropBox.Min.Y - offset.Y,
                level.Elevation);

            XYZ maxProjectPoint = new(
                cropBox.Max.X - offset.X,
                cropBox.Max.Y - offset.Y,
                level.Elevation);

            return new Outline(minProjectPoint, maxProjectPoint);
        }

        /// <summary>
        /// Получает базовую точку проекта, которая используется для преобразования координат
        /// </summary>
        private BasePoint GetProjectBasePoint(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(BasePoint)).Cast<BasePoint>()
                .FirstOrDefault(bp => !bp.IsShared);
        }

        /// <summary>
        /// Обработка сложной геометрии границ обрезки вида
        /// </summary>
        private Outline GetCropRegionOutline(IList<CurveLoop> cropShapes, Level level)
        {
            List<XYZ> allBoundaryPoints = [];

            foreach (CurveLoop curveLoop in cropShapes)
            {
                foreach (Curve curve in curveLoop)
                {
                    // Извлечение точек из различных типов кривых
                    List<XYZ> points = ExtractPointsFromCurve(curve);
                    allBoundaryPoints.AddRange(points);
                }
            }

            if (!allBoundaryPoints.Any())
            {
                return null;
            }

            // Преобразование в проектные координаты
            BasePoint basePoint = GetProjectBasePoint(level.Document);
            XYZ offset = basePoint?.Position ?? XYZ.Zero;

            List<XYZ> transformedPoints = [.. allBoundaryPoints.Select(pt => new XYZ(pt.X - offset.X, pt.Y - offset.Y, pt.Z))];

            // Определение границ области
            XYZ minProjectPoint = new(
                transformedPoints.Min(p => p.X),
                transformedPoints.Min(p => p.Y),
                level.Elevation);

            XYZ maxProjectPoint = new(
                transformedPoints.Max(p => p.X),
                transformedPoints.Max(p => p.Y),
                level.Elevation);

            return new Outline(minProjectPoint, maxProjectPoint);
        }

        /// <summary>
        /// Извлечение точек из различных типов кривых
        /// </summary>
        private List<XYZ> ExtractPointsFromCurve(Curve curve)
        {
            List<XYZ> points =
            [
                // Базовые точки кривой
                curve.GetEndPoint(0),
                curve.GetEndPoint(1)
            ];

            // Дополнительная дискретизация для сложных кривых
            if (curve is Arc or Ellipse or NurbSpline)
            {
                const int subdivisions = 10;
                for (int i = 1; i < subdivisions; i++)
                {
                    double parameter = (double)i / subdivisions;
                    points.Add(curve.Evaluate(parameter, false));
                }
            }

            return points;
        }




        public LogicalOrFilter CreateIntersectFilter(Document doc, FloorModel current, List<FloorModel> floorModels, bool visible = false)
        {
            double clearance = UnitManager.MmToFoot(100);

            double height = GetLevelHeight(current, floorModels, out double elevation);

            XYZ minPoint = Transform.Identity.OfPoint(new XYZ(MinX, MinY, elevation + clearance));

            XYZ maxPoint = Transform.Identity.OfPoint(new XYZ(MaxX, MaxY, elevation + height));

            Solid floorSolid = SolidHelper.CreateSolidBoxByPoint(minPoint, maxPoint, height);

            Outline outline = new(minPoint, maxPoint);

            if (visible)
            {
                floorSolid.CreateDirectShape(doc);
            }

            ElementIntersectsSolidFilter solidFilter = new(floorSolid);
            BoundingBoxIntersectsFilter boundingBoxFilter = new(outline);

            return new LogicalOrFilter(boundingBoxFilter, solidFilter);
        }


        public static double GetLevelHeight(FloorModel current, List<FloorModel> floors, out double elevation)
        {
            double result = 0;

            elevation = current.ProjectElevation;

            List<FloorModel> sortedFloors = [.. floors.OrderBy(x => x.ProjectElevation)];
            FloorModel aboveFloor = sortedFloors.FirstOrDefault(x => x.ProjectElevation > current.ProjectElevation);
            FloorModel belowFloor = sortedFloors.LastOrDefault(x => x.ProjectElevation < current.ProjectElevation);

            if (current.FloorNumber > 0 && aboveFloor is not null && belowFloor is not null)
            {
                result = Math.Abs(aboveFloor.ProjectElevation - current.ProjectElevation);
            }
            else if (current.FloorNumber > 1 && aboveFloor is null)
            {
                result = Math.Abs(current.ProjectElevation - belowFloor.ProjectElevation);
            }
            else if (current.FloorNumber < 0 && belowFloor is null)
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
