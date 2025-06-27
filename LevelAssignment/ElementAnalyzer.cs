namespace LevelAssignment
{
    public class ElementAnalyzer
    {
        private readonly Document _document;
        private readonly double _basePointZ;

        public ElementAnalyzer(Document document)
        {
            _document = document;
            _basePointZ = GetBasePointZ();
        }

        /// <summary>
        /// Получает элементы, поддерживающие заданный параметр
        /// </summary>
        public List<Element> GetElementsWithParameter(Guid parameterGuid)
        {
            return [.. new FilteredElementCollector(_document)
                .WhereElementIsNotElementType()
                .Cast<Element>()
                .Where(e => e.get_Parameter(parameterGuid) != null &&
                           !e.get_Parameter(parameterGuid).IsReadOnly)];
        }

        /// <summary>
        /// Вычисляет пространственные характеристики элементов
        /// </summary>
        public List<ElementSpatialData> CalculateElementsSpatialData(List<Element> elements)
        {
            return [.. elements.Select(element => new ElementSpatialData
            {
                Element = element,
                MinZ = CalculateElementMinZ(element),
                BoundingBox = element.get_BoundingBox(null)
            })];
        }

        private double CalculateElementMinZ(Element element)
        {
            var bbox = element.get_BoundingBox(null);
            if (bbox == null) return 0;

            double heightOffset = GetParameterDoubleValue(element, BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            return Math.Round((bbox.Min.Z - _basePointZ + heightOffset) * 304.8 + 1);
        }

        private double GetBasePointZ()
        {
            return new FilteredElementCollector(_document)
                .OfClass(typeof(BasePoint))
                .Cast<BasePoint>()
                .FirstOrDefault(bp => !bp.IsShared)?
                .Position.Z ?? 0;
        }

        private double GetParameterDoubleValue(Element element, BuiltInParameter paramId)
        {
            ElementId typeId = element.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                Parameter param = element.Document.GetElement(typeId).get_Parameter(paramId);
                return param?.AsDouble() ?? 0;
            }
            return 0;
        }
    }

    public class ElementSpatialData
    {
        public Element Element { get; set; }
        public double MinZ { get; set; }
        public BoundingBoxXYZ BoundingBox { get; set; }
    }



}
