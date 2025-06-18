namespace LevelAssignment
{
    internal class LevelDeterminator
    {
        // Получение отсортированных по высоте уровней
        public List<Level> GetSortedLevels(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(lev => lev.Elevation).ToList();
        }

        // Базовый алгоритм фильтрации по уровню
        public ICollection<Element> GetElementsOnLevel(Document doc, ElementId levelId)
        {
            ElementLevelFilter levelFilter = new(levelId);
            return new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(levelFilter)
                .ToElements();
        }


    }

    /// <summary>
    /// Главный класс для определения уровней элементов
    /// </summary>
    public class ElementLevelDeterminator
    {
        private readonly Document _document;
        private readonly IOrderedEnumerable<Level> _sortedLevels;

        public ElementLevelDeterminator(Document doc)
        {
            _document = doc;
            _sortedLevels = GetSortedLevels(doc);
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
                    result.AssignedLevel = _document.GetElement(element.LevelId) as Level;
                    result.Method = LevelDeterminationMethod.AssignedLevel;
                    result.Confidence = 1.0;
                    return result;
                }

                // Этап 2: Параметрический анализ
                var parameterLevel = GetLevelFromParameters(element);
                if (parameterLevel != null)
                {
                    result.AssignedLevel = parameterLevel;
                    result.Method = LevelDeterminationMethod.ParameterBased;
                    result.Confidence = 0.9;
                    return result;
                }

                // Этап 3: Геометрический анализ
                var geometricLevel = GetLevelFromGeometry(element);
                if (geometricLevel != null)
                {
                    result.AssignedLevel = geometricLevel;
                    result.Method = LevelDeterminationMethod.GeometricAnalysis;
                    result.Confidence = 0.8;
                    return result;
                }

                // Этап 4: Пространственный анализ (через помещения)
                var spatialLevel = DetermineLevelFromSpatialRelationship(element, _document);
                if (spatialLevel != null)
                {
                    result.AssignedLevel = spatialLevel;
                    result.Method = LevelDeterminationMethod.SpatialAnalysis;
                    result.Confidence = 0.7;
                    return result;
                }

                result.Method = LevelDeterminationMethod.Failed;
                result.Confidence = 0.0;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.Method = LevelDeterminationMethod.Error;
            }

            return result;
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
        public LevelDeterminationMethod Method { get; set; }
        public double Confidence { get; set; }
        public string Error { get; set; }
    }

    public enum LevelDeterminationMethod
    {
        AssignedLevel,
        ParameterBased,
        GeometricAnalysis,
        SpatialAnalysis,
        Failed,
        Error
    }


}
