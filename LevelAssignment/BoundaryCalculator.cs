﻿using Autodesk.Revit.DB;
using CommonUtils;
using RevitUtils;
using System.Text;

namespace LevelAssignment
{
    internal sealed class BoundaryCalculator(IModuleLogger logger)
    {
        private readonly IModuleLogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private double MinX { get; set; } = double.MaxValue;
        private double MaxX { get; set; } = double.MinValue;
        private double MinY { get; set; } = double.MaxValue;
        private double MaxY { get; set; } = double.MinValue;
        private double MinZ { get; set; } = double.MaxValue;
        private double MaxZ { get; set; } = double.MinValue;

        /// <summary>
        /// Вычисление общей границы проекта на основе планов этажей
        /// </summary>
        public Outline ComputeProjectBoundary(Document doc, List<FloorData> floorDataList)
        {
            StringBuilder logBuilder = new();
            List<Outline> boundaryOutlines = [];

            _ = logBuilder.AppendLine($"🔍 Computing project boundary...");

            HashSet<ElementId> viewsOnSheets = GetViewsOnValidSheets(doc);

            List<FloorData> sortedFloors = [.. floorDataList.OrderBy(x => x.BaseElevation)];

            foreach (FloorData floorInfo in sortedFloors)
            {
                floorInfo.Height = GetLevelHeight(floorInfo, sortedFloors);

                List<Outline> floorOutlines = ExtractFloorBoundaries(doc, floorInfo, viewsOnSheets);

                if (floorOutlines.Count > 0)
                {
                    boundaryOutlines.AddRange(floorOutlines);
                }
            }

            if (boundaryOutlines.Count == 0)
            {
                _logger.Warning("⚠️ No boundaries found !");
                throw new InvalidOperationException("⚠️ No boundaries found!");
            }

            _ = logBuilder.AppendLine($"📋 Found {viewsOnSheets.Count} views on valid sheets");
            _ = logBuilder.AppendLine($"📐 Total boundaries collected: {boundaryOutlines.Count}");
            _ = logBuilder.AppendLine("🎯 Project boundary computed successfully");

            _logger.Information(logBuilder.ToString());

            return MergeOutlines(boundaryOutlines);
        }

        /// <summary>
        /// Обработка границ для одного этажа
        /// </summary>
        private static List<Outline> ExtractFloorBoundaries(Document doc, FloorData floorData, HashSet<ElementId> viewsOnSheets)
        {
            List<Outline> outlines = [];

            foreach (ElementId levelId in floorData.ContainedLevelIds)
            {
                Element levelElement = doc.GetElement(levelId);

                if (levelElement is Level level)
                {
                    foreach (ViewPlan floorPlan in GetViewPlansByLevel(doc, level))
                    {
                        if (!floorPlan.IsCallout && viewsOnSheets.Contains(floorPlan.Id))
                        {
                            Outline boundary = ExtractViewPlanBoundary(floorPlan, floorData.BaseElevation);

                            if (boundary != null)
                            {
                                outlines.Add(boundary);
                            }
                        }
                    }
                }
            }

            return outlines;
        }

        /// <summary>
        /// Извлекает границы плана этажа с применением приоритетной стратегии
        /// </summary>
        internal static Outline ExtractViewPlanBoundary(ViewPlan floorPlan, double elevation)
        {
            // Стратегия 1: CropBox 
            if (floorPlan.CropBoxActive && floorPlan.CropBox != null)
            {
                return TransformCropBox(floorPlan, elevation);
            }

            // Стратегия 2: View Outline
            if (floorPlan.Outline != null)
            {
                return TransformViewOutline(floorPlan, elevation);
            }

            // Стратегия 3: Model boundary transform
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
        /// Получает высоту уровня относительно других уровней
        /// </summary>
        private static double GetLevelHeight(FloorData current, List<FloorData> sortedFloors)
        {
            FloorData aboveFloor = sortedFloors.FirstOrDefault(x => x.BaseElevation > current.BaseElevation);
            FloorData belowFloor = sortedFloors.LastOrDefault(x => x.BaseElevation < current.BaseElevation);

            if (aboveFloor != null && belowFloor != null)
            {
                return Math.Abs(aboveFloor.BaseElevation - current.BaseElevation);
            }

            if (current.FloorIndex > 1 && aboveFloor is null)
            {
                return UnitManager.MmToFoot(3500);
            }

            if (current.FloorIndex < 0 && belowFloor is null)
            {
                current.BaseElevation -= UnitManager.MmToFoot(5000);
                return Math.Abs(aboveFloor.BaseElevation - current.BaseElevation);
            }

            return 0;
        }

        /// <summary>
        /// Получает все виды, которые находятся на листах
        /// </summary>
        private static HashSet<ElementId> GetViewsOnValidSheets(Document doc)
        {
            FilteredElementCollector collector;
            HashSet<ElementId> validViews = [];

            collector = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSheet))
                        .WhereElementIsNotElementType()
                        .OfCategory(BuiltInCategory.OST_Sheets);

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
        private static List<ViewPlan> GetViewPlansByLevel(Document doc, Level level)
        {
            return [.. new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .OfType<ViewPlan>()
                .Where(pln => !pln.IsTemplate && pln.GenLevel.Id == level.Id)];
        }

        /// <summary>
        /// Преобразует границы CropBox в проектные координаты
        /// </summary>
        private static Outline TransformCropBox(View view, double elevation)
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
        private static Outline TransformViewOutline(View view, double elevation)
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
        private static Transform GetViewTransform(View view)
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
        private static List<XYZ> ExtractPoints(Curve curve, Transform trans)
        {
            return
            [
                trans.OfPoint(curve.GetEndPoint(0)),
                trans.OfPoint(curve.GetEndPoint(1)),
            ];
        }


    }
}