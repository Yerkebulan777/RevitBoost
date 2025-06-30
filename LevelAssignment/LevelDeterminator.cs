
using Autodesk.Revit.DB.Architecture;
using System.Diagnostics;

namespace LevelAssignment
{
    /// <summary>
    /// Главный класс для определения уровней элементов
    /// </summary>
    public class LevelDeterminator
    {
        /// <summary>
        /// Определение принадлежность к уровню 
        /// </summary>
        public bool IsOnLevel(Element element, ref HashSet<ElementId> levelIds)
        {
            Debug.Assert(!levelIds.Any(), "No levels provided for checking!");

            Parameter levelParam;

            if (levelIds.Any())
            {
                string categoryName = element.Category.Name;

                if (element.LevelId != ElementId.InvalidElementId)
                {
                    Debug.WriteLine($"0 LEVEL_ID: {categoryName}");
                    return levelIds.Contains(element.LevelId);
                }

                if (element is Wall)
                {
                    levelParam = element.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                    if (levelParam?.AsElementId() is ElementId id && levelIds.Contains(id))
                    {
                        Debug.WriteLine($"1 WALL_BASE_CONSTRAINT: {categoryName}");
                        return true;
                    }
                }

                if (element is Stairs)
                {
                    levelParam = element.get_Parameter(BuiltInParameter.STAIRS_BASE_LEVEL_PARAM);
                    if (levelParam?.AsElementId() is ElementId id && levelIds.Contains(id))
                    {
                        Debug.WriteLine($"1 STAIRS_BASE_LEVEL_PARAM: {categoryName}");
                        return true;
                    }
                }

                if (element is RoofBase)
                {
                    levelParam = element.get_Parameter(BuiltInParameter.ROOF_BASE_LEVEL_PARAM);
                    if (levelParam?.AsElementId() is ElementId id && levelIds.Contains(id))
                    {
                        Debug.WriteLine($"1 ROOF_BASE_LEVEL_PARAM: {categoryName}");
                        return true;
                    }
                }

                levelParam = element.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                if (levelParam?.AsElementId() is ElementId baseId && levelIds.Contains(baseId))
                {
                    Debug.WriteLine($"1 LEVEL_PARAM: {categoryName}");
                    return true;
                }

                levelParam = element.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
                if (levelParam?.AsElementId() is ElementId scheduleId && levelIds.Contains(scheduleId))
                {
                    Debug.WriteLine($"2 SCHEDULE_LEVEL_PARAM: {categoryName}");
                    return true;
                }

                levelParam = element.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                if (levelParam?.AsElementId() is ElementId familyId && levelIds.Contains(familyId))
                {
                    Debug.WriteLine($"3 FAMILY_LEVEL_PARAM: {categoryName}");
                    return true;
                }

                throw new InvalidOperationException($"Не удалось определить уровень для {categoryName}!");
            }

            return false;

        }

        /// <summary>
        /// Checks if a point is within the vertical boundaries of a bounding box
        /// </summary>
        public bool IsPointInVerticalBounds(XYZ point, BoundingBoxXYZ bbox)
        {
            return point.Z > bbox.Min.Z && point.Z < bbox.Max.Z;
        }

        /// <summary>
        /// Определяет этаж на основе геометрического анализа высоты элемента
        /// </summary>
        public bool DetermineFloorByGeometry(ElementSpatialData elementData, ref List<FloorInfo> sortedFloors)
        {
            // Поиск подходящего этажа по высоте
            for (int idx = 0; idx < sortedFloors.Count; idx++)
            {
                FloorInfo floor = sortedFloors[idx];

                if (IsPointInVerticalBounds(elementData.Centroid, floor.BoundingBox))
                {
                    return true;
                }
            }

            throw new InvalidDataException($"Не удалось определить этаж для элемента по геометрии!");
        }
    }

    /// <summary>
    /// Данные о пространственных характеристиках элемента
    /// </summary>
    public record ElementSpatialData
    {
        public XYZ Centroid { get; set; }
        public Element Element { get; set; }
        public BoundingBoxXYZ BoundingBox { get; set; }
    }

    /// <summary>
    /// Результат определения уровня элемента
    /// </summary>
    public record LevelAssignmentResult
    {
        public readonly Element Element;
        public float Confidence { get; set; }
        public string ErrorMessage { get; set; }
        public Determination Method { get; set; }
        public bool IsSuccess { get; set; }
        public LevelAssignmentResult(Element element)
        {
            Method = Determination.Failed;
            Element = element;
            Confidence = 0;
        }
    }



    public enum Determination
    {
        ParameterBased,
        GeometricAnalysis,
        SpatialAnalysis,
        Failed,
    }



}