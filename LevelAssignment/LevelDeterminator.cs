
using Autodesk.Revit.DB.Architecture;
using System.Diagnostics;

namespace LevelAssignment
{
    /// <summary>
    /// Главный класс для определения уровней элементов
    /// </summary>
    public class ElementLevelDeterminator
    {
        private readonly Document _document;
        private readonly List<Level> _levels;

        public ElementLevelDeterminator(Document doc, List<Level> levels)
        {
            _document = doc;
            _levels = levels;
        }

        /// <summary>
        /// Основной метод определения уровня элемента
        /// </summary>
        public LevelAssignmentResult DetermineElementLevel(Element element)
        {
            LevelAssignmentResult result = new(element);

            try
            {
                // Этап 1: Проверка назначенного уровня

                Level geometricLevel = GetLevelFromGeometry(element);

                if (geometricLevel != null)
                {
                    result.Method = Determination.GeometricAnalysis;
                    result.Confidence = 1;
                    return result;
                }



            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in geometric analysis for element {element.Id}: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Определение принадлежность к уровню 
        /// </summary>
        public bool IsOnLevel(Element element, ref HashSet<ElementId> levelIds)
        {
            Parameter baseLevel;

            if (!levelIds.Any())
            {
                Debug.Fail("No levels provided for checking!");
                return false;
            }

            string categoryName = element.Category.Name;

            if (element.LevelId != ElementId.InvalidElementId)
            {
                Debug.WriteLine($"LEVEL_ID: {categoryName}");
                return levelIds.Contains(element.LevelId);
            }

            if (element is Wall wall)
            {
                baseLevel = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                if (baseLevel?.AsElementId() is ElementId id && levelIds.Contains(id))
                {
                    Debug.WriteLine($"WALL_BASE_CONSTRAINT: {categoryName}");
                    return true;
                }
            }

            if (element is RoofBase)
            {
                baseLevel = element.get_Parameter(BuiltInParameter.ROOF_BASE_LEVEL_PARAM);
                if (baseLevel?.AsElementId() is ElementId id && levelIds.Contains(id))
                {
                    Debug.WriteLine($"ROOF_BASE_LEVEL_PARAM: {categoryName}");
                    return true;
                }
            }

            if (element is Stairs)
            {
                baseLevel = element.get_Parameter(BuiltInParameter.STAIRS_BASE_LEVEL_PARAM);
                if (baseLevel?.AsElementId() is ElementId id && levelIds.Contains(id))
                {
                    Debug.WriteLine($"STAIRS_BASE_LEVEL_PARAM: {categoryName}");
                    return true;
                }
            }

            baseLevel = element.get_Parameter(BuiltInParameter.LEVEL_PARAM);
            if (baseLevel?.AsElementId() is ElementId baseId && levelIds.Contains(baseId))
            {
                Debug.WriteLine($"LEVEL_PARAM: {categoryName}");
                return true;
            }

            baseLevel = element.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
            if (baseLevel?.AsElementId() is ElementId scheduleId && levelIds.Contains(scheduleId))
            {
                Debug.WriteLine($"SCHEDULE_LEVEL_PARAM: {categoryName}");
                return true;
            }

            baseLevel = element.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
            if (baseLevel?.AsElementId() is ElementId familyId && levelIds.Contains(familyId))
            {
                Debug.WriteLine($"FAMILY_LEVEL_PARAM: {categoryName}");
                return true;
            }

            throw new InvalidOperationException($"Не удалось определить уровень для {categoryName}!");

        }

        /// <summary>
        /// Определение уровня на основе геометрического анализа
        /// </summary>
        private Level GetLevelFromGeometry(Element element)
        {
            try
            {
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);

                if (bbox == null)
                {
                    return null;
                }

            }
            catch (Exception)
            {
                // В случае ошибки возвращаем null
            }

            return null;
        }


        /// <summary>
        /// Пакетная обработка элементов
        /// </summary>
        public Dictionary<ElementId, LevelAssignmentResult> ProcessElements(IEnumerable<Element> elements)
        {
            return elements.ToDictionary(elem => elem.Id, DetermineElementLevel);
        }
    }

    /// <summary>
    /// Результат определения уровня элемента
    /// </summary>
    public record LevelAssignmentResult
    {
        public readonly Element Element;
        public int Confidence { get; set; }
        public Determination Method { get; set; }

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