using Autodesk.Revit.DB;
using CommonUtils;
using RevitUtils;

namespace LevelAssignment
{
    internal sealed class BoundaryCalculator
    {
        private readonly IModuleLogger _logger;
        public BoundaryCalculator(IModuleLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

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
            _logger.Information("Computing boundary for {FloorCount} floors", floorModels.Count);

            List<Outline> floorPlanOutlines = [];
            HashSet<ElementId> viewsOnSheets = GetViewsOnValidSheets(doc);

            _logger.Debug("Found {ViewCount} views on valid sheets", viewsOnSheets.Count);

            foreach (FloorInfo floorModel in floorModels)
            {
                floorModel.Height = GetLevelHeight(floorModel, floorModels, out double elevation);

                int boundariesFound = 0;
                int levelsProcessed = 0;

                foreach (ElementId levelId in floorModel.ContainedLevelIds)
                {
                    if (doc.GetElement(levelId) is Level level)
                    {
                        levelsProcessed++;
                        _logger.Debug("Processing level {LevelName}", level.Name);
                        List<ViewPlan> floorPlans = GetViewPlansByLevel(doc, level);
                        int validPlans = 0;

                        foreach (ViewPlan floorPlan in floorPlans)
                        {
                            bool isCallout = floorPlan.IsCallout;
                            bool onSheet = viewsOnSheets.Contains(floorPlan.Id);

                            if (!isCallout && onSheet)
                            {
                                validPlans++;

                                _logger.Debug("Valid plan found: {PlanName}", floorPlan.Name);
                                Outline boundary = ExtractViewPlanBoundary(floorPlan, elevation);

                                if (boundary is not null)
                                {
                                    floorPlanOutlines.Add(boundary);
                                    boundariesFound++;
                                }
                                else
                                {
                                    _logger.Warning("No boundary extracted from {PlanName}", floorPlan.Name);
                                }
                            }
                        }

                        _logger.Debug("Level {LevelName}: {ValidPlans} valid plans of {TotalPlans}", level.Name, validPlans, floorPlans.Count);
                    }
                }

                _logger.Debug("Floor {FloorIndex} summary: {LevelsProcessed} levels, {BoundariesFound} boundaries", floorModel.Index, levelsProcessed, boundariesFound);
            }

            _logger.Information("Total boundaries collected: {TotalBoundaries}", floorPlanOutlines.Count);

            if (floorPlanOutlines.Count == 0)
            {
                _logger.Warning("No boundaries found - using default outline");
                return new Outline(XYZ.Zero, new XYZ(100, 100, 100));
            }

            Outline result = MergeOutlines(floorPlanOutlines);

            return result;
        }

        /// <summary>
        /// Обновление границ проекта на основе контуров
        /// </summary>
        internal Outline MergeOutlines(List<Outline> outlines)
        {
            _logger.Debug("Merging {Count} outlines", outlines.Count);

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
            _logger.Debug("Extracting boundary from view plan {ViewName}", floorPlan.Name);

            // Стратегия 1: Использование активного CropBox
            if (floorPlan.CropBoxActive && floorPlan.CropBox != null)
            {
                _logger.Debug("Using CropBox strategy for view {ViewName}", floorPlan.Name);
                return TransformCropBox(floorPlan, elevation);
            }

            // Стратегия 2: Использование свойства GeometryOutline вида
            if (floorPlan.Outline != null)
            {
                _logger.Debug("Using View Outline strategy for view {ViewName}", floorPlan.Name);
                return TransformViewOutline(floorPlan, elevation);
            }

            // Стратегия 3: Model boundary transform
            _logger.Debug("Using Model Transform strategy for view {ViewName}", floorPlan.Name);
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

                _logger.Debug("Extracted boundary from {PointCount} points for view {ViewName}", boundaryPoints.Count, floorPlan.Name);

                return new Outline(minProjectPoint, maxProjectPoint);
            }

            _logger.Warning("Could not extract boundary from view {ViewName}", floorPlan.Name);

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

            _logger.Debug("Calculating height for floor {FloorIndex}", current.Index);

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
        private List<ViewPlan> GetViewPlansByLevel(Document doc, Level level)
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
        /// Преобразование границ GeometryOutline в проектные координаты
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
