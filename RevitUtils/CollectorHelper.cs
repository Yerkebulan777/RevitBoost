using Autodesk.Revit.DB;
using System.Text;
using Document = Autodesk.Revit.DB.Document;


namespace RevitUtils
{
    public static class CollectorHelper
    {
        /// <summary>
        /// Получает список уровней, которые имеют высоту меньше заданного максимума
        /// </summary>
        public static List<Level> GetSortedLevels(Document doc, double elevationLimitMeters = 100)
        {
            FilterNumericLess evaluator = new();
            ParameterValueProvider provider = new(new ElementId(BuiltInParameter.LEVEL_ELEV));
            double maximum = UnitManager.MmToFoot((elevationLimitMeters * 1000) - 1000);
            FilterDoubleRule rule = new(provider, evaluator, maximum, 5E-3);

            return [.. new FilteredElementCollector(doc).OfClass(typeof(Level))
                .WherePasses(new ElementParameterFilter(rule)).Cast<Level>()
                .OrderBy(x => x.Elevation)];
        }

        /// <summary>
        /// Безопасная работа с коллектором с применением фильтров
        /// </summary>
        public static (FilteredElementCollector, string) DiagnoseWithFilters(Document doc, params ElementFilter[] filters)
        {
            StringBuilder builder = new();

            FilteredElementCollector collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();

            builder.AppendLine($"Initial elements...");

            foreach (ElementFilter filter in filters)
            {
                collector = collector.WherePasses(filter);
                builder.AppendLine($"Filter: {filter.GetType().Name}");
                builder.AppendLine($"Elements after filter: {collector.GetElementCount()}");
            }

            return (collector, builder.ToString());
        }

        /// <summary>
        /// Собирает элементы по категории и имени символа
        /// </summary>
        public static FilteredElementCollector CollectInstancesBySymbolName(Document doc, BuiltInCategory bic, string symbolName)
        {
            ElementId typeNameParamId = new(BuiltInParameter.ALL_MODEL_TYPE_NAME);
            ElementId familyNameParamId = new(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM);

            FilterRule typeRule = ParameterFilterRuleFactory.CreateContainsRule(typeNameParamId, symbolName);
            FilterRule symbolRule = ParameterFilterRuleFactory.CreateContainsRule(familyNameParamId, symbolName);

            LogicalOrFilter logicOrFilter = new(new ElementParameterFilter(typeRule), new ElementParameterFilter(symbolRule));

            return new FilteredElementCollector(doc).OfCategory(bic).WherePasses(logicOrFilter).WhereElementIsNotElementType();
        }

        /// <summary>
        /// Создает фильтр по конкретному параметру уровня
        /// </summary>
        public static ElementParameterFilter BuildNumericParameterFilter(BuiltInParameter bip, FilterNumericRuleEvaluator evaluator, ElementId levelId)
        {
            ParameterValueProvider valueProvider = new(new ElementId(bip));
            FilterElementIdRule filterRule = new(valueProvider, evaluator, levelId);
            return new ElementParameterFilter(filterRule);
        }


        #region Extensions

        public static FilteredElementCollector WhereHasParameterValue(this FilteredElementCollector collector, Parameter parameter)
        {
            HasValueFilterRule rule = new(parameter.Id);
            ElementParameterFilter filter = new(rule);
            return collector.WherePasses(filter);
        }

        public static FilteredElementCollector ExcludeElements(this FilteredElementCollector collector, ICollection<ElementId> elementIds)
        {
            return elementIds?.Count > 0 ? collector.WherePasses(new ExclusionFilter(elementIds)) : collector;
        }

        public static FilteredElementCollector WhereHasParameter(this FilteredElementCollector collector, string parameterName)
        {
            SharedParameterApplicableRule rule = new(parameterName);
            ElementParameterFilter filter = new(rule);
            return collector.WherePasses(filter);
        }

        #endregion



    }
}
