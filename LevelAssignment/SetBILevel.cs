using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;

namespace LevelAssignment
{
    [Transaction(TransactionMode.Manual)]
    public class SetBILevel : IExternalCommand
    {
        /// <summary>
        /// GUID параметра BI_этаж (разделяемый параметр)
        /// </summary>
        private static readonly Guid BILevelParameterGuid = new("4673f045-9574-471f-9677-ac538a9e9a2d");

        /// <summary>
        /// Толщина перекрытия для расчета смещения (в мм)
        /// </summary>
        private const double TFloors = 100.0;

        /// <summary>
        /// Точка входа для внешней команды Revit
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Получение текущего документа
                Document document = commandData.Application.ActiveUIDocument.Document;

                using (Transaction transaction = new Transaction(document))
                {
                    transaction.Start("Заполнение BI_этаж по нижней границе");
                    string result = Main(document, BILevelParameterGuid);
                    transaction.Commit();

                    // Отображение результата выполнения
                    TaskDialog.Show("Результат", result);
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.ToString();
                TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Основная логика работы команды
        /// </summary>
        public string Main(Document doc, Guid biLevelParamGuid)
        {
            // Проверка наличия параметра BI_этаж в проекте
            if (!CheckParameterExists(doc, biLevelParamGuid))
                return "Ошибка: В проекте не существует параметра BI_этаж";

            // Получение всех элементов с возможностью установки BI_этаж
            List<Element> modelElements = GetModelElements(doc, biLevelParamGuid);
            if (modelElements.Count == 0)
                return "Ошибка: Нет элементов для обработки";

            // Получение всех уровней проекта
            List<Level> levels = GetProjectLevels(doc);

            // Получение координаты Z базовой точки проекта
            double basePointZ = GetBasePointZ(doc);

            // Вычисление минимальных Z-координат для элементов
            List<double> minPointZ = CalculateMinZCoordinates(modelElements, basePointZ);

            // Получение отсортированных уровней с высотами
            var (sortedLevelNumbers, sortedLevelHeights) = GetSortedLevelsAndHeights(doc, levels, TFloors);

            // Определение номеров уровней для элементов
            List<int> levelNumbersForElements = DetermineLevelNumbers(
                sortedLevelNumbers, sortedLevelHeights, modelElements, minPointZ);

            // Установка параметров BI_этаж для элементов
            SetBiLevelParameters(modelElements, levelNumbersForElements, biLevelParamGuid);

            return "✅ Заполнение параметров BI_этаж выполнено успешно";
        }

        /// <summary>
        /// Проверяет наличие параметра BI_этаж в проекте
        /// </summary>
        private bool CheckParameterExists(Document doc, Guid paramGuid)
        {
            return new FilteredElementCollector(doc).WhereElementIsNotElementType().Any(e => e.get_Parameter(paramGuid) != null);
        }

        /// <summary>
        /// Получает элементы, поддерживающие параметр BI_этаж
        /// </summary>
        private List<Element> GetModelElements(Document doc, Guid biLevelParamGuid)
        {
            return new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .Cast<Element>()
                .Where(e =>
                    e.get_Parameter(biLevelParamGuid) != null &&
                    !e.get_Parameter(biLevelParamGuid).IsReadOnly)
                .ToList();
        }

        /// <summary>
        /// Получает все уровни проекта
        /// </summary>
        private List<Level> GetProjectLevels(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType()
                .Cast<Level>()
                .ToList();
        }

        /// <summary>
        /// Получает координату Z базовой точки проекта
        /// </summary>
        private double GetBasePointZ(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(BasePoint))
                .Cast<BasePoint>()
                .FirstOrDefault(bp => !bp.IsShared)?
                .Position.Z ?? 0;
        }

        /// <summary>
        /// Вычисляет минимальные Z-координаты для элементов
        /// </summary>
        private List<double> CalculateMinZCoordinates(List<Element> elements, double basePointZ)
        {
            return elements.Select(e =>
                Math.Round(
                    (e.get_BoundingBox(null).Min.Z - basePointZ +
                    GetParameterDoubleValue(e, BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)) * 304.8 + 1))
                .ToList();
        }

        /// <summary>
        /// Получает отсортированные уровни с учетом смещений перекрытий
        /// </summary>
        private (List<int> levelNumbers, List<double> levelHeights) GetSortedLevelsAndHeights(
            Document doc, List<Level> levels, double floorThickness)
        {
            // Извлечение номеров уровней из имен
            List<int> levelNumbers = levels
                .Select(l => ToIntFirstNumberInText(l.Name, new[] { " ", "_" }))
                .ToList();

            // Извлечение высот уровней
            List<double> levelHeights = levels
                .Select(l => Math.Round(l.Elevation * 304.8))
                .ToList();

            // Получение смещений перекрытий
            List<double> floorOffsets = GetFloorOffsets(doc, levels, floorThickness);

            // Применение смещений
            levelHeights = levelHeights.Zip(floorOffsets, (h, o) => h + o).ToList();

            // Сортировка по высоте
            var sortedPairs = levelHeights
                .Select((h, i) => new { Height = h, Index = i })
                .OrderBy(p => p.Height)
                .ToList();

            // Формирование отсортированных списков
            levelNumbers = sortedPairs.Select(p => levelNumbers[p.Index]).ToList();
            levelHeights = sortedPairs.Select(p => p.Height).ToList();

            return (levelNumbers, levelHeights);
        }

        /// <summary>
        /// Получает смещения перекрытий для уровней
        /// </summary>
        private List<double> GetFloorOffsets(Document doc, List<Level> levels, double floorThickness)
        {
            return levels
                .Select(l => GetLargestFloorAtLevel(doc, l))
                .Select(f => f == null ? -floorThickness :
                    Math.Round(f.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).AsDouble() * 304.8))
                .ToList();
        }

        /// <summary>
        /// Находит самое большое перекрытие на уровне
        /// </summary>
        /// <summary>
        /// Находит самое большое перекрытие на уровне
        /// </summary>
        private Floor GetLargestFloorAtLevel(Document doc, Level level)
        {
            ElementId levelId = level.Id;
            List<Floor> floors = new FilteredElementCollector(doc)
                .OfClass(typeof(Floor))
                .Cast<Floor>()
                .Where(f =>
                {
                    Parameter levelParam = f.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                    if (levelParam != null)
                    {
                        ElementId paramId = levelParam.AsElementId();
                        return paramId != null && paramId.Compare(levelId) == 0;
                    }
                    return false;
                })
                .ToList();

            return floors.Count == 0 ? null : floors
                .OrderByDescending(f => f.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble() ?? 0)
                .FirstOrDefault();
        }

        /// <summary>
        /// Определяет номер уровня для каждого элемента
        /// </summary>
        private List<int> DetermineLevelNumbers(
            List<int> sortedLevelNumbers, List<double> sortedLevelHeights,
            List<Element> elements, List<double> minPointZ)
        {
            List<int> levelNumbers = new List<int>();

            for (int i = 0; i < elements.Count; i++)
            {
                double z = minPointZ[i];

                if (z < sortedLevelHeights[1])
                {
                    levelNumbers.Add(sortedLevelNumbers[0]);
                }
                else if (z >= sortedLevelHeights.Last())
                {
                    levelNumbers.Add(sortedLevelNumbers.Last());
                }
                else
                {
                    for (int j = 1; j < sortedLevelNumbers.Count - 1; j++)
                    {
                        if (z >= sortedLevelHeights[j] && z < sortedLevelHeights[j + 1])
                        {
                            levelNumbers.Add(sortedLevelNumbers[j]);
                            break;
                        }
                    }
                }
            }

            return levelNumbers;
        }

        /// <summary>
        /// Устанавливает значение параметра BI_этаж для элементов
        /// </summary>
        private void SetBiLevelParameters(
            List<Element> elements, List<int> levelNumbers, Guid biLevelParamGuid)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                Parameter param = elements[i].get_Parameter(biLevelParamGuid);
                if (param != null && !param.IsReadOnly)
                {
                    param.Set(levelNumbers[i]);
                }
            }
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
        /// Получает строковое значение параметра элемента
        /// </summary>
        private string GetParameterValue(Element element, Guid paramGuid)
        {
            Parameter param = element.get_Parameter(paramGuid);
            if (param != null && !string.IsNullOrEmpty(param.AsString()))
                return param.AsString();

            ElementId typeId = element.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                param = element.Document.GetElement(typeId).get_Parameter(paramGuid);
                if (param != null && !string.IsNullOrEmpty(param.AsString()))
                    return param.AsString();
            }
            return string.Empty;
        }

        /// <summary>
        /// Получает строковое значение параметра элемента
        /// </summary>
        private string GetParameterValue(Element element, BuiltInParameter paramId)
        {
            Parameter param = element.get_Parameter(paramId);
            return param?.AsString() ?? string.Empty;
        }
    }
}