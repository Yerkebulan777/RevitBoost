using RevitUtils;

namespace LevelAssignment
{
    internal sealed class ProjectBoundaryCalculator
    {
        private double MinX { get; set; } = double.MaxValue;
        private double MaxX { get; set; } = double.MinValue;
        private double MinY { get; set; } = double.MaxValue;
        private double MaxY { get; set; } = double.MinValue;
        private double MinZ { get; set; } = double.MaxValue;
        private double MaxZ { get; set; } = double.MinValue;

        public Outline ProjectBoundaryOutline { get; private set; }

        /// <summary>
        /// Получение границ видов планов до 3 этажа
        /// </summary>
        public void CalculateBoundingPoints(Document doc, List<FloorModel> floorModels)
        {
            List<FloorModel> filteredFloors = [.. floorModels.Where(fm => fm.FloorNumber <= 3)];

            foreach (FloorModel floorModel in filteredFloors)
            {
                List<Outline> floorPlanOutlines = [];

                foreach (Level level in floorModel.ContainedLevels)
                {
                    foreach (ViewPlan floorPlan in GetViewPlansByLevel(doc, level))
                    {
                        // Надо добавить проверку  нахождения на листе//

                        //foreach (ElementId idVp in sheet.GetAllViewports())
                        //{
                        //    Viewport vp = doc.GetElement(idVp) as Viewport;
                        //    if (vp is not null && vp.ViewId == floorPlan.Id)
                        //    {
                        //        // Пропускаем планы, которые уже находятся на листе
                        //        continue;
                        //    }
                        //}

                        Outline viewBoundary = ExtractViewPlanBoundary(floorPlan, 0);

                        if (viewBoundary is not null)
                        {
                            floorPlanOutlines.Add(viewBoundary);
                        }
                    }
                }

                Outline mergedOutline = ProcessBoundaries(floorPlanOutlines);
                // Также я бы подумал об высоте viewBoundary
                floorModel.BoundaryOutline = mergedOutline;
            }
        }

        /// <summary>
        /// Обработка границ
        /// </summary>
        private Outline ProcessBoundaries(List<Outline> outlines)
        {
            foreach (Outline outline in outlines)
            {
                MinX = Math.Min(MinX, outline.MinimumPoint.X);
                MaxX = Math.Max(MaxX, outline.MaximumPoint.X);
                MinY = Math.Min(MinY, outline.MinimumPoint.Y);
                MaxY = Math.Max(MaxY, outline.MaximumPoint.Y);
                MinZ = Math.Min(MinZ, outline.MinimumPoint.Z);
                MaxZ = Math.Max(MaxZ, outline.MaximumPoint.Z);
            }

            XYZ minPoint = new(MinX, MinY, MinZ);
            XYZ maxPoint = new(MaxX, MaxY, MaxZ);

            return new Outline(minPoint, maxPoint);
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
        /// Извлекает границы плана этажа с применением приоритетной стратегии
        /// </summary>
        internal Outline ExtractViewPlanBoundary(ViewPlan floorPlan, double elevation)
        {
            // Стратегия 1: Использование активного CropBox
            if (floorPlan.CropBoxActive && floorPlan.CropBox != null)
            {
                return TransformCropBox(floorPlan, elevation);
            }

            // Стратегия 2: Использование свойства Outline вида
            if (floorPlan.Outline != null)
            {
                return TransformViewOutline(floorPlan, elevation);
            }

            // Стратегия 3: ...
            List<XYZ> boundaryPoints = [];

            foreach (TransformWithBoundary twb in floorPlan.GetModelToProjectionTransforms())
            {
                Transform trans = twb.GetModelToProjectionTransform();

                CurveLoop boundary = twb.GetBoundary();

                if (boundary is not null)
                {
                    foreach (Curve curve in boundary)
                    {
                        boundaryPoints.AddRange(ExtractPoints(curve, trans));
                    }
                }
            }

            if (!boundaryPoints.Any())
            {
                return null;
            }

            XYZ minProjectPoint = new(
                boundaryPoints.Min(p => p.X),
                boundaryPoints.Min(p => p.Y),
                elevation);

            XYZ maxProjectPoint = new(
                boundaryPoints.Max(p => p.X),
                boundaryPoints.Max(p => p.Y),
                elevation);

            return new Outline(minProjectPoint, maxProjectPoint);
        }

        /// <summary>
        /// Преобразует границы CropBox в проектные координаты
        /// </summary>
        private Outline TransformCropBox(View view, double elevation)
        {
            BoundingBoxXYZ cropBox = view.CropBox;
            Transform viewTransform = view.CropBox.Transform;

            XYZ projectMin = viewTransform.OfPoint(cropBox.Min);
            XYZ projectMax = viewTransform.OfPoint(cropBox.Max);

            XYZ minPoint = new(projectMin.X, projectMin.Y, elevation);
            XYZ maxPoint = new(projectMax.X, projectMax.Y, elevation);

            return new Outline(minPoint, maxPoint);
        }

        /// <summary>
        /// Преобразование mergedOutline вида в проектные координаты
        /// </summary>
        private Outline TransformViewOutline(View view, double elevation)
        {
            BoundingBoxUV viewOutline = view.Outline;

            Transform viewTransform = GetViewTransform(view);

            XYZ minUV = new(viewOutline.Min.U, viewOutline.Min.V, 0);
            XYZ maxUV = new(viewOutline.Max.U, viewOutline.Max.V, 0);

            XYZ minPoint = viewTransform.OfPoint(minUV);
            XYZ maxPoint = viewTransform.OfPoint(maxUV);

            minPoint = new XYZ(minPoint.X, minPoint.Y, elevation);
            maxPoint = new XYZ(maxPoint.X, maxPoint.Y, elevation);

            return new Outline(minPoint, maxPoint);
        }

        /// <summary>
        /// Получение трансформации вида
        /// </summary>
        private Transform GetViewTransform(View view)
        {
            Transform trans = Transform.Identity;
            trans.BasisX = view.RightDirection;
            trans.BasisY = view.UpDirection;
            trans.BasisZ = view.ViewDirection;
            trans.Origin = view.Origin;

            return trans;
        }

        /// <summary>
        /// Извлечение точек из различных типов кривых
        /// </summary>
        private List<XYZ> ExtractPoints(Curve curve, Transform trans)
        {
            List<XYZ> points =
            [
                trans.OfPoint(curve.GetEndPoint(0)),
                trans.OfPoint(curve.GetEndPoint(1)),
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
