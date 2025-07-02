using System.Diagnostics;

namespace LevelAssignment
{
    /// <summary>
    /// Главный класс для определения уровней элементов
    /// </summary>
    public class LevelDeterminator
    {
        /// <summary>
        /// Определяет этаж на основе геометрического анализа высоты элемента
        /// </summary>
        public bool IsElementOnFloorByGeometry(int Index, Element element, IList<FloorInfo> sortedFloors)
        {
            BoundingBoxXYZ bbox = element.get_BoundingBox(null);

            XYZ center = (bbox.Min + bbox.Max) * 0.5;

            for (int idx = 0; idx < sortedFloors.Count; idx++)
            {
                FloorInfo floor = sortedFloors[idx];

                BoundingBoxXYZ bounding = floor.BoundingBox;

                if (floor.Index >= Index && IsPointContained(center, bounding))
                {
                    return true;
                }
            }

            throw new InvalidDataException($"Не удалось определить этаж для элемента по геометрии!");
        }

        /// <summary>
        /// Определяет, находится ли точка в пределах BoundingBox
        /// </summary>
        public bool IsPointContained(XYZ point, BoundingBoxXYZ bbox)
        {
            Outline outline = new(bbox.Min, bbox.Max);
            return outline.Contains(point, double.Epsilon);
        }
    }





}