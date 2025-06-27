
namespace LevelAssignment
{
    /// <summary>
    /// Главный класс для определения уровней элементов
    /// </summary>
    public class ElementLevelDeterminator
    {
        private readonly Document _document;
        private readonly List<Level> _levels;
        private const double TFloors = 100.0; // Толщина перекрытия в мм

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
            LevelAssignmentResult result = new() { Element = element };

            try
            {
                // Этап 1: Проверка назначенного уровня
                if (element.LevelId != ElementId.InvalidElementId)
                {
                    result.Method = Determination.AssignedLevel;
                    result.Confidence = 1.0;
                    return result;
                }

                // Этап 2: Параметрический анализ
                Level parameterLevel = GetLevelFromParameters(element);
                if (parameterLevel != null)
                {
                    result.Method = Determination.ParameterBased;
                    result.Confidence = 0.9;
                    return result;
                }

                // Этап 3: Геометрический анализ
                Level geometricLevel = GetLevelFromGeometry(element);
                if (geometricLevel != null)
                {
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
        public bool GetLevelFromParameters(Element element, HashSet<ElementId> levelIds)
        {

            // Этап 1: Проверка назначенного уровня
            if (element.LevelId != ElementId.InvalidElementId)
            {
                return levelIds.Contains(element.LevelId);
            }

            else if (element is FamilyInstance instance)
            {
                // Проверяем параметр уровня основания для семейных экземпляров
                Parameter baseLevel = instance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
                return levelIds.Contains(baseLevel.AsElementId());
            }

            else if (element is HostObject host)
            {
                // Проверяем параметр уровня размещения для хост-объектов
                Parameter hostLevel = host.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                return levelIds.Contains(hostLevel.AsElementId());
            }

            return false;
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


                // Вычисляем минимальную Z-координату элемента с учетом смещений
                double minZ = CalculateElementMinZ(element, basePointZ);

                // Получаем отсортированные уровни с высотами
                var (levelNumbers, levelHeights) = GetSortedLevelsAndHeights();

                // Находим подходящий уровень
                int levelIndex = DetermineLevelIndex(minZ, levelHeights);

                if (levelIndex >= 0 && levelIndex < _levels.Count)
                {
                    return _levels[levelIndex];
                }
            }
            catch (Exception)
            {
                // В случае ошибки возвращаем null
            }

            return null;
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
                {
                    return param.AsDouble();
                }
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
                DetermineElementLevel
            );
        }
    }

    /// <summary>
    /// Результат определения уровня элемента
    /// </summary>
    public class LevelAssignmentResult
    {
        public Element Element { get; set; }
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