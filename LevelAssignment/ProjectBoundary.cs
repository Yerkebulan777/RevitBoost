using RevitUtils;

namespace LevelAssignment
{
    internal sealed class ProjectBoundary
    {
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }

        /// <summary>
        /// Определяет границы проекта на основе видимых элементов этажей
        /// </summary>

        public void CalculateBoundingPoints(Document doc, List<Level> levels, double minimum)
        {
            List<ElementId> modelCategoryIds = CollectorHelper.GetModelCategoryIds(doc, GetExcludedCategories());

            HashSet<ElementId> visibleElementIds = [];

            double buffer = UnitManager.MmToFoot(300);

            foreach (Level currentLevel in levels)
            {
                MinX -= buffer; MinY -= buffer;
                MaxX += buffer; MaxY += buffer;

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

        /// <summary>
        /// Получение границ видов планов с приоритетной проверкой CropBoxActive
        /// </summary>
        public void CalculateBoundingPoints(Document doc, List<Level> levels)
        {
            List<Outline> prioritizedOutlines = [];

            foreach (Level level in levels)
            {
                List<ViewPlan> floorPlans = GetViewPlansByLevel(doc, level);

                foreach (ViewPlan floorPlan in floorPlans)
                {
                    Outline viewBoundary = ExtractViewBoundaryWithPriority(floorPlan, level);

                    if (viewBoundary != null)
                    {
                        prioritizedOutlines.Add(viewBoundary);
                    }
                }
            }

            ProcessPrioritizedBoundaries(prioritizedOutlines);
        }

        private Outline ExtractViewBoundaryWithPriority(ViewPlan floorPlan, Level level)
        {
            // Приоритет 1: Активный CropBox
            if (floorPlan.CropBoxActive && floorPlan.CropBox != null)
            {
                return ConvertCropBoxToProjectOutline(floorPlan.CropBox, level);
            }

            // Приоритет 2: Свойство Outline вида (если доступно)
            try
            {
                BoundingBoxUV viewOutline = floorPlan.Outline;
                if (viewOutline != null)
                {
                    return TransformViewOutlineToProjectCoordinates(viewOutline, level);
                }
            }
            catch (Exception)
            {
                // Продолжаем к следующему методу если Outline недоступен
            }

            // Приоритет 3: Анализ через CropRegionShapeManager
            ViewCropRegionShapeManager cropManager = floorPlan.GetCropRegionShapeManager();

            if (cropManager.CanHaveShape)
            {
                IList<CurveLoop> cropShapes = cropManager.GetCropShape();

                if (cropShapes.Any())
                {
                    return ProcessCropShapeGeometry(cropShapes, level);
                }
            }

            return null;
        }

        private Outline TransformToProjectCoordinates(Outline viewOutline, Level level)
        {
            BasePoint basePoint = GetProjectBasePoint(level.Document);

            XYZ offset = basePoint.Position;

            XYZ minPoint = new(
                viewOutline.MinimumPoint.X - offset.X,
                viewOutline.MinimumPoint.Y - offset.Y,
                level.Elevation);

            XYZ maxPoint = new(
                viewOutline.MaximumPoint.X - offset.X,
                viewOutline.MaximumPoint.Y - offset.Y,
                level.Elevation);

            return new Outline(minPoint, maxPoint);
        }


        private Outline ConvertCropBoxToProjectOutline(BoundingBoxXYZ cropBox, Level level)
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

        private BasePoint GetProjectBasePoint(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(BasePoint)).Cast<BasePoint>()
                .FirstOrDefault(bp => !bp.IsShared);
        }


        /// <summary>
        /// Обработка сложной геометрии границ обрезки вида
        /// </summary>
        private Outline ProcessCropShapeGeometry(IList<CurveLoop> cropShapes, Level level)
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

        /// <summary>
        /// Получает список исключенных категорий категорий  
        /// </summary>
        /// <returns></returns>
        private static List<BuiltInCategory> GetExcludedCategories()
        {
            List<BuiltInCategory> excludedCategories =
            [
                // Арматура и армирование
                BuiltInCategory.OST_Rebar,
                BuiltInCategory.OST_PathRein,
                BuiltInCategory.OST_FabricReinforcement,
                BuiltInCategory.OST_StructuralStiffener,

                // MEP фитинги и соединения
                BuiltInCategory.OST_PipeFitting,
                BuiltInCategory.OST_DuctFitting,
                BuiltInCategory.OST_CableTrayFitting,
                BuiltInCategory.OST_ConduitFitting,
                BuiltInCategory.OST_ConnectorElem,

                // Детализация и аннотации
                BuiltInCategory.OST_DetailComponents,
                BuiltInCategory.OST_GenericAnnotation,
                BuiltInCategory.OST_Entourage,
                BuiltInCategory.OST_Site
            ];

            return excludedCategories;
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
        /// Обновляет границы ограничивающего прямоугольника
        /// </summary>
        private void UpdateBoundingLimits(XYZ minPoint, XYZ maxPoint)
        {
            MinX = Math.Min(MinX, minPoint.X);
            MinY = Math.Min(MinY, minPoint.Y);
            MaxX = Math.Max(MaxX, maxPoint.X);
            MaxY = Math.Max(MaxY, maxPoint.Y);
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
