using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;

namespace LevelAssignment
{
    /// <summary>
    /// Команда для автоматического заполнения параметров BI_этажа для вертикальных конструкций в паркинге
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    internal class SetBILevelForParking : IExternalCommand
    {
        // Константы для высотных отметок
        private const double ReferenceHeight1 = 3300.0; // Высота первого уровня (мм)
        private const double ReferenceHeight2 = 6000.0; // Высота второго уровня (мм)

        // GUID параметров
        private static readonly Guid BILevelParameterGuid = new("4673f045-9574-471f-9677-ac538a9e9a2d");
        private static readonly Guid BIHeightParameterGuid = new("ace912eb-094e-4fd8-a1c7-ccaada7682d3");
        private static readonly Guid BIMarkParameterGuid = new("2204049c-d557-4dfc-8d70-13f19715e45d");

        // Марки вертикальных конструкций
        private static readonly string[] VerticalMarks = { "СТм", "СЦм", "СЖм", "Км" };
        private const string ColumnsMark = "Км";

        /// <summary>
        /// Точка входа для внешней команды Revit
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Document document = commandData.Application.ActiveUIDocument.Document;

                // Получение элементов вертикальных конструкций
                List<Element> verticalElements = GetVerticalElements(document);
                if (verticalElements.Count == 0)
                {
                    TaskDialog.Show("Результат", "В проекте не найдены подходящие вертикальные конструкции");
                    return Result.Succeeded;
                }

                // Получение высот элементов
                List<double> elementHeights = GetElementHeights(verticalElements);

                // Основная логика установки параметров
                using (Transaction transaction = new Transaction(document))
                {
                    transaction.Start("Заполнение BI_этаж для паркинга");

                    // Запуск основного алгоритма из общего класса
                    SetBILevel setBiLevel = new SetBILevel();
                    string result = setBiLevel.Main(document, BILevelParameterGuid);

                    // Установка параметров для бетонных конструкций
                    SetConcreteParameters(verticalElements, elementHeights);

                    // Установка параметров для арматуры
                    SetRebarParameters(document, verticalElements, elementHeights);

                    transaction.Commit();

                    // Отображение результата
                    TaskDialog.Show("Результат", $"{result}\n✅ Заполнение параметров BI_этажа выполнено успешно");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Получает вертикальные конструкции из проекта
        /// </summary>
        private List<Element> GetVerticalElements(Document doc)
        {
            List<Element> verticalElements = new List<Element>();

            // Колонны и стены
            var wallCollector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .Cast<Element>()
                .Where(e => IsContainsInList(e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString(), VerticalMarks))
                .ToList();

            var columnCollector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType()
                .Cast<Element>()
                .Where(e => e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString().Contains(ColumnsMark))
                .ToList();

            verticalElements.AddRange(wallCollector);
            verticalElements.AddRange(columnCollector);

            return verticalElements;
        }

        /// <summary>
        /// Получает высоты элементов
        /// </summary>
        private List<double> GetElementHeights(List<Element> elements)
        {
            List<double> heights = new List<double>();

            foreach (var element in elements)
            {
                if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralColumns)
                {
                    // Для колонн используем пользовательский параметр высоты
                    Parameter heightParam = element.get_Parameter(BIHeightParameterGuid);
                    if (heightParam != null)
                        heights.Add(double.Parse(heightParam.AsValueString()));
                }
                else
                {
                    // Для стен используем встроенный параметр высоты
                    Parameter heightParam = element.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
                    if (heightParam != null)
                        heights.Add(double.Parse(heightParam.AsValueString()));
                }
            }

            return heights;
        }

        /// <summary>
        /// Устанавливает параметры BI_этаж для бетонных конструкций
        /// </summary>
        private void SetConcreteParameters(List<Element> elements, List<double> heights)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                Parameter levelParam = elements[i].get_Parameter(BILevelParameterGuid);
                if (levelParam == null || levelParam.IsReadOnly)
                    continue;

                double currentLevel = levelParam.AsDouble();
                double height = heights[i];

                // Определяем корректировку уровня на основе высоты элемента
                double levelAdjustment;
                if (height >= ReferenceHeight1 && height < ReferenceHeight2)
                {
                    levelAdjustment = currentLevel < 0 ? -0.2 : 0.2;
                }
                else if (height >= ReferenceHeight2)
                {
                    levelAdjustment = currentLevel < 0 ? -0.3 : 0.3;
                }
                else
                {
                    levelAdjustment = currentLevel < 0 ? -0.1 : 0.1;
                }

                // Применяем корректировку
                levelParam.Set(currentLevel + levelAdjustment);
            }
        }

        /// <summary>
        /// Устанавливает параметры BI_этаж для арматуры
        /// </summary>
        private void SetRebarParameters(Document doc, List<Element> verticalElements, List<double> verticalHeights)
        {
            // Получаем арматуру с подходящими марками
            List<Element> rebars = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rebar)
                .WhereElementIsNotElementType()
                .Cast<Element>()
                .Where(e => IsContainsInList(e.get_Parameter(BIMarkParameterGuid).AsString(), VerticalMarks))
                .ToList();

            if (rebars.Count == 0) return;

            // Сортируем вертикальные элементы по высоте
            var sortedElements = verticalElements
                .Zip(verticalHeights, (element, height) => new { Element = element, Height = height })
                .OrderBy(x => x.Height)
                .Select(x => x.Element)
                .ToList();

            // Устанавливаем уровень арматуры по соответствующей конструкции
            foreach (var rebar in rebars)
            {
                Parameter levelParam = rebar.get_Parameter(BILevelParameterGuid);
                if (levelParam == null || levelParam.IsReadOnly)
                    continue;

                string mark = rebar.get_Parameter(BIMarkParameterGuid).AsString();
                foreach (var element in sortedElements)
                {
                    if (mark == element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString() &&
                        BoundingBoxesAndSolidIntersect(rebar, element))
                    {
                        double level = element.get_Parameter(BILevelParameterGuid).AsDouble();
                        levelParam.Set(level);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Проверяет, содержит ли строка одну из заданных марок
        /// </summary>
        public static bool IsContainsInList(string text, string[] marks)
        {
            if (string.IsNullOrEmpty(text)) return false;

            return marks.Any(mark => text.Contains(mark));
        }

        /// <summary>
        /// Проверяет пересечение элементов по BoundingBox и геометрии
        /// </summary>
        public static bool BoundingBoxesAndSolidIntersect(Element e1, Element e2)
        {
            BoundingBoxXYZ bb1 = e1.get_BoundingBox(null);
            BoundingBoxXYZ bb2 = e2.get_BoundingBox(null);

            if (bb1 == null || bb2 == null) return false;

            Outline outline1 = new Outline(bb1.Min, bb1.Max);
            Outline outline2 = new Outline(bb2.Min, bb2.Max);

            return outline1.Intersects(outline2, 0.0);
        }

        /// <summary>
        /// Проверяет пересечение геометрии элементов
        /// </summary>
        public static bool CheckElementsIntersect(Element e1, Element e2)
        {
            Solid solid1 = GetSolidFromElement(e1);
            Solid solid2 = GetSolidFromElement(e2);

            if (solid1 == null || solid2 == null) return false;

            try
            {
                Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                    solid1, solid2, BooleanOperationsType.Intersect);

                return intersection != null && intersection.Volume > 1e-6;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Получает геометрию элемента
        /// </summary>
        public static Solid GetSolidFromElement(Element element)
        {
            Options options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geometryElement = element.get_Geometry(options);
            if (geometryElement == null) return null;

            foreach (GeometryObject geomObj in geometryElement)
            {
                if (geomObj is Solid solid && solid.Volume > 1e-6)
                    return solid;

                if (geomObj is GeometryInstance instance)
                {
                    foreach (GeometryObject instanceGeom in instance.GetInstanceGeometry())
                    {
                        if (instanceGeom is Solid instanceSolid && instanceSolid.Volume > 1e-6)
                            return instanceSolid;
                    }
                }
            }

            return null;
        }
    }
}