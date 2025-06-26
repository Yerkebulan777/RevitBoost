using RevitUtils;
using System.Diagnostics;

namespace LevelAssignment
{
    internal sealed class BoundaryCalculator
    {
        private double MinX { get; set; }
        private double MaxX { get; set; }
        private double MinY { get; set; }
        private double MaxY { get; set; }

        private BasePoint _cachedBasePoint;

        public Outline ProjectBoundaryOutline { get; private set; }

        /// <summary>
        /// Кэшированное получение базовой точки проекта
        /// </summary>
        private BasePoint GetProjectBasePoint(Document doc)
        {
            return _cachedBasePoint ??= new FilteredElementCollector(doc)
                .OfClass(typeof(BasePoint))
                .Cast<BasePoint>()
                .FirstOrDefault(bp => !bp.IsShared);
        }

        /// <summary>
        /// Извлекает границы плана этажа с применением приоритетной стратегии
        /// </summary>
        internal Outline ExtractViewPlanBoundary(ViewPlan floorPlan, Level level)
        {
            try
            {
                // Стратегия 1: Использование активного CropBox
                if (floorPlan.CropBoxActive && floorPlan.CropBox != null)
                {
                    return TransformCropBox(floorPlan.CropBox, level);
                }

                // Стратегия 2: Использование свойства Outline вида
                if (floorPlan.Outline != null)
                {
                    return TransformViewOutline(floorPlan.Outline, level);
                }

                // Стратегия 3: Анализ через CropRegionShapeManager
                var cropManager = floorPlan.GetCropRegionShapeManager();

                if (cropManager?.CanHaveShape == true)
                {
                    var cropShapes = cropManager.GetCropShape();

                    if (cropShapes?.Any() == true)
                    {
                        return GetCropRegionOutline(cropShapes, level);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при извлечении границ плана {floorPlan.Name}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Преобразование outline вида в проектные координаты
        /// </summary>
        private Outline TransformViewOutline(BoundingBoxUV viewOutline, Level level)
        {
            var minPoint = new XYZ(viewOutline.Min.U, viewOutline.Min.V, level.Elevation);
            var maxPoint = new XYZ(viewOutline.Max.U, viewOutline.Max.V, level.Elevation);
            return new Outline(minPoint, maxPoint);
        }

        /// <summary>
        /// Преобразует границы CropBox в проектные координаты
        /// </summary>
        private Outline TransformCropBox(BoundingBoxXYZ cropBox, Level level)
        {
            var minPoint = new XYZ(cropBox.Min.X, cropBox.Min.Y, level.Elevation);
            var maxPoint = new XYZ(cropBox.Max.X, cropBox.Max.Y, level.Elevation);
            return new Outline(minPoint, maxPoint);
        }

        /// <summary>
        /// Обработка сложной геометрии границ обрезки вида
        /// </summary>
        private Outline GetCropRegionOutline(IList<CurveLoop> cropShapes, Level level)
        {
            var allBoundaryPoints = new List<XYZ>();

            foreach (var curveLoop in cropShapes)
            {
                foreach (var curve in curveLoop)
                {
                    allBoundaryPoints.AddRange(ExtractPointsFromCurve(curve));
                }
            }

            if (!allBoundaryPoints.Any()) return null;

            var minProjectPoint = new XYZ(
                allBoundaryPoints.Min(p => p.X),
                allBoundaryPoints.Min(p => p.Y),
                level.Elevation);

            var maxProjectPoint = new XYZ(
                allBoundaryPoints.Max(p => p.X),
                allBoundaryPoints.Max(p => p.Y),
                level.Elevation);

            return new Outline(minProjectPoint, maxProjectPoint);
        }


        /// <summary>
        /// Извлечение точек из различных типов кривых
        /// </summary>
        private List<XYZ> ExtractPointsFromCurve(Curve curve)
        {
            // Базовые точки 
            List<XYZ> points =
            [
                curve.GetEndPoint(0),
                curve.GetEndPoint(1),
            ];

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
