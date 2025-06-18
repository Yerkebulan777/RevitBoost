using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LevelAssignment
{
    /// <summary>
    /// Главный класс для определения уровней элементов
    /// </summary>
    public class ElementLevelDeterminator
    {
        private readonly Document doc;
        private readonly List<Level> _sortedLevels;
        private const double TFloors = 100.0; // Толщина перекрытия в мм
        private const double Tolerance = 0.003; // Допуск для геометрических расчетов

        public ElementLevelDeterminator(Document doc)
        {
            this.doc = doc;
            _sortedLevels = GetSortedLevels(doc);
        }

        /// <summary>
        /// Получение отсортированных по высоте уровней
        /// </summary>
        public List<Level> GetSortedLevels(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(lev => lev.Elevation).ToList();
        }

        /// <summary>
        /// Базовый алгоритм фильтрации по уровню
        /// </summary>
        public ICollection<Element> GetElementsOnLevel(Document doc, ElementId levelId)
        {
            ElementLevelFilter levelFilter = new ElementLevelFilter(levelId);
            return new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(levelFilter)
                .ToElements();
        }

        /// <summary>
        /// Основной метод определения уровня элемента
        /// </summary>
        public LevelAssignmentResult DetermineElementLevel(Element element)
        {
            var result = new LevelAssignmentResult { Element = element };

            try
            {
                // Этап 1: Проверка назначенного уровня
                if (element.LevelId != ElementId.InvalidElementId)
                {
                    result.AssignedLevel = doc.GetElement(element.LevelId) as Level;
                    result.Method = Determination.AssignedLevel;
                    result.Confidence = 1.0;
                    return result;
                }

                // Этап 2: Параметрический анализ
                var parameterLevel = GetLevelFromParameters(element);
                if (parameterLevel != null)
                {
                    result.AssignedLevel = parameterLevel;
                    result.Method = Determination.ParameterBased;
                    result.Confidence = 0.9;
                    return result;
                }

                // Этап 3: Геометрический анализ
                var geometricLevel = GetLevelFromGeometry(element);
                if (geometricLevel != null)
                {
                    result.AssignedLevel = geometricLevel;
                    result.Method = Determination.GeometricAnalysis;
                    result.Confidence = 0.8;
                    return result;
                }

                result.Method = Determination.Failed;
                result.Confidence = 0.0;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.Method = Determination.Error;
            }

            return result;
        }

        /// <summary>
        /// Определение уровня на основе параметров элемента
        /// </summary>
        private Level GetLevelFromParameters(Element element)
        {
            try
            {
                // Проверяем параметр уровня основания
                Parameter baseLevel = element.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
                if (baseLevel?.AsElementId() != null && baseLevel.AsElementId() != ElementId.InvalidElementId)
                {
                    return doc.GetElement(baseLevel.AsElementId()) as Level;
                }

                // Проверяем параметр уровня размещения
                Parameter placementLevel = element.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                if (placementLevel?.AsElementId() != null && placementLevel.AsElementId() != ElementId.InvalidElementId)
                {
                    return doc.GetElement(placementLevel.AsElementId()) as Level;
                }

                // Для стен проверяем базовый и верхний уровни
                if (element is Wall wall)
                {
                    Parameter wallBaseLevel = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                    if (wallBaseLevel?.AsElementId() != null && wallBaseLevel.AsElementId() != ElementId.InvalidElementId)
                    {
                        return doc.GetElement(wallBaseLevel.AsElementId()) as Level;
                    }
                }
            }
            catch (Exception)
            {
                // В случае ошибки возвращаем null
            }

            return null;
        }

        /// <summary>
        /// Определение уровня на основе геометрического анализа
        /// </summary>
        private Level GetLevelFromGeometry(Element element)
        {
            try
            {
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                if (bbox == null) return null;

                // Получаем базовую точку проекта
                double basePointZ = GetBasePointZ();

                // Вычисляем минимальную Z-координату элемента с учетом смещений
                double minZ = CalculateElementMinZ(element, basePointZ);

                // Получаем отсортированные уровни с высотами
                var (levelNumbers, levelHeights) = GetSortedLevelsAndHeights();

                // Находим подходящий уровень
                int levelIndex = DetermineLevelIndex(minZ, levelHeights);

                if (levelIndex >= 0 && levelIndex < _sortedLevels.Count)
                {
                    return _sortedLevels[levelIndex];
                }
            }
            catch (Exception)
            {
                // В случае ошибки возвращаем null
            }

            return null;
        }

        /// <summary>
        /// Получает координату Z базовой точки проекта
        /// </summary>
        private double GetBasePointZ()
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(BasePoint))
                .Cast<BasePoint>()
                .FirstOrDefault(bp => !bp.IsShared)?
                .Position.Z ?? 0;
        }

        /// <summary>
        /// Вычисляет минимальную Z-координату элемента
        /// </summary>
        private double CalculateElementMinZ(Element element, double basePointZ)
        {
            BoundingBoxXYZ bbox = element.get_BoundingBox(null);
            if (bbox == null) return 0;

            double heightOffset = GetParameterDoubleValue(element, BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            return Math.Round((bbox.Min.Z - basePointZ + heightOffset) * 304.8 + 1);
        }

        /// <summary>
        /// Получает отсортированные уровни с высотами
        /// </summary>
        private (List<int> levelNumbers, List<double> levelHeights) GetSortedLevelsAndHeights()
        {
            // Извлечение номеров уровней из имен
            List<int> levelNumbers = _sortedLevels
                .Select(l => ToIntFirstNumberInText(l.Name, new[] { " ", "_" }))
                .ToList();

            // Извлечение высот уровней
            List<double> levelHeights = _sortedLevels
                .Select(l => Math.Round(l.Elevation * 304.8))
                .ToList();

            // Получение смещений перекрытий
            List<double> floorOffsets = GetFloorOffsets();

            // Применение смещений
            levelHeights = levelHeights.Zip(floorOffsets, (h, o) => h + o).ToList();

            // Сортировка по высоте
            var sortedPairs = levelHeights
                .Select((h, i) => new { Height = h, Index = i })
                .OrderBy(p => p.Height)
                .ToList();

            return (
                sortedPairs.Select(p => levelNumbers[p.Index]).ToList(),
                sortedPairs.Select(p => p.Height).ToList()
            );
        }

        /// <summary>
        /// Получает смещения перекрытий для уровней
        /// </summary>
        private List<double> GetFloorOffsets()
        {
            return _sortedLevels
                .Select(GetLargestFloorAtLevel)
                .Select(f => f == null ? -TFloors :
                    Math.Round(f.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).AsDouble() * 304.8))
                .ToList();
        }

        /// <summary>
        /// Находит самое большое перекрытие на уровне
        /// </summary>
        private Floor GetLargestFloorAtLevel(Level level)
        {
            ElementId levelId = level.Id;
            List<Floor> floors = new FilteredElementCollector(doc)
                .OfClass(typeof(Floor))
                .Cast<Floor>()
                .Where(f =>
                {
                    Parameter levelParam = f.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                    return levelParam?.AsElementId()?.Compare(levelId) == 0;
                })
                .ToList();

            return floors.Count == 0 ? null : floors
                .OrderByDescending(f => f.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble() ?? 0)
                .FirstOrDefault();
        }

        /// <summary>
        /// Определяет индекс уровня по Z-координате
        /// </summary>
        private int DetermineLevelIndex(double z, List<double> levelHeights)
        {
            if (levelHeights.Count < 2) return 0;

            if (z < levelHeights[1])
                return 0;

            if (z >= levelHeights.Last())
                return levelHeights.Count - 1;

            for (int i = 1; i < levelHeights.Count - 1; i++)
            {
                if (z >= levelHeights[i] && z < levelHeights[i + 1])
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Преобразует первую числовую часть строки в целое число
        /// </summary>
        private int ToIntFirstNumberInText(string text, string[] separators)
        {
            foreach (string part in text.Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part, out int result))
                    return result;
            }
            return 0;
        }

        /// <summary>
        /// Получает числовое значение параметра из типа элемента
        /// </summary>
        private double GetParameterDoubleValue(Element element, BuiltInParameter paramId)
        {
            ElementId typeId = element.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                Parameter param = element.Document.GetElement(typeId).get_Parameter(paramId);
                if (param != null)
                    return param.AsDouble();
            }
            return 0;
        }

        /// <summary>
        /// Пакетная обработка элементов
        /// </summary>
        public Dictionary<ElementId, LevelAssignmentResult> ProcessElements(IEnumerable<Element> elements)
        {
            return elements.ToDictionary(
                elem => elem.Id,
                elem => DetermineElementLevel(elem)
            );
        }
    }

    /// <summary>
    /// Результат определения уровня элемента
    /// </summary>
    public class LevelAssignmentResult
    {
        public Element Element { get; set; }
        public Level AssignedLevel { get; set; }
        public Determination Method { get; set; }
        public double Confidence { get; set; }
        public string Error { get; set; }
    }

    public enum Determination
    {
        AssignedLevel,
        ParameterBased,
        GeometricAnalysis,
        SpatialAnalysis,
        Failed,
        Error
    }



}