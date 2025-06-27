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

        /// <summary>
        /// Вычисление общей границы проекта на основе планов этажей
        /// </summary>
        public Outline ComputeProjectBoundary(Document doc, ref List<FloorInfo> floorModels)
        {
            List<Outline> floorPlanOutlines = [];

            HashSet<ElementId> viewsOnSheets = GetViewsOnValidSheets(doc);

            foreach (FloorInfo floorModel in floorModels)
            {
                floorModel.Height = GetLevelHeight(floorModel, floorModels, out double elevation);

                foreach (Level level in floorModel.ContainedLevels)
                {
                    foreach (ViewPlan floorPlan in GetViewPlansByLevel(doc, level))
                    {
                        if (!floorPlan.IsCallout && viewsOnSheets.Contains(floorPlan.Id))
                        {
                            Outline boundary = ExtractViewPlanBoundary(floorPlan, elevation);

                            if (boundary is not null)
                            {
                                floorPlanOutlines.Add(boundary);
                            }
                        }
                    }
                }
            }

            return MergeOutlines(floorPlanOutlines);
        }

        /// <summary>
        /// Обновление границ проекта на основе контуров
        /// </summary>
        internal Outline MergeOutlines(List<Outline> outlines)
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

                foreach (Curve curve in twb.GetBoundary())
                {
                    boundaryPoints.AddRange(ExtractPoints(curve, trans));
                }
            }

            if (boundaryPoints.Any())
            {
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

            return null;
        }

        /// <summary>
        /// Получает высоту уровня относительно других уровней
        /// </summary>
        private double GetLevelHeight(FloorInfo current, List<FloorInfo> floors, out double elevation)
        {
            double result = 0;

            elevation = current.InternalElevation;

            List<FloorInfo> sortedFloors = [.. floors.OrderBy(x => x.InternalElevation)];

            FloorInfo aboveFloor = sortedFloors.FirstOrDefault(x => x.InternalElevation > current.InternalElevation);
            FloorInfo belowFloor = sortedFloors.LastOrDefault(x => x.InternalElevation < current.InternalElevation);

            if (current.Index > 0 && aboveFloor is not null && belowFloor is not null)
            {
                result = Math.Abs(aboveFloor.InternalElevation - current.InternalElevation);
            }
            else if (current.Index > 1 && aboveFloor is null)
            {
                result = Math.Abs(current.InternalElevation - belowFloor.InternalElevation);
            }
            else if (current.Index < 0 && belowFloor is null)
            {
                result = Math.Abs(aboveFloor.InternalElevation - current.InternalElevation);
                double subtract = UnitManager.MmToFoot(3000);
                elevation -= subtract;
                result += subtract;
            }

            return result;
        }

        /// <summary>
        /// Получает все виды, которые находятся на листах
        /// </summary>
        private HashSet<ElementId> GetViewsOnValidSheets(Document doc)
        {
            HashSet<ElementId> validViews = [];

            FilteredElementCollector collector = new(doc);
            collector = collector.OfClass(typeof(ViewSheet));
            collector = collector.WhereElementIsNotElementType();
            collector = collector.OfCategory(BuiltInCategory.OST_Sheets);

            foreach (ViewSheet sheet in collector.Cast<ViewSheet>())
            {
                if (!sheet.IsPlaceholder && sheet.CanBePrinted)
                {
                    validViews.UnionWith(sheet.GetAllPlacedViews());
                }
            }

            return validViews;
        }

        /// <summary>
        /// Получает все планы этажей для указанного уровня
        /// </summary>
        List<ViewPlan> GetViewPlansByLevel(Document doc, Level level)
        {
            return [.. new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan)).OfType<ViewPlan>()
                .Where(pln => !pln.IsTemplate && pln.GenLevel.Id == level.Id)];
        }

        /// <summary>
        /// Преобразует границ CropBox в проектные координаты
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
        /// Преобразование границ Outline в проектные координаты
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



    }
}
