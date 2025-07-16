using Autodesk.Revit.DB;
using System.Diagnostics;
using System.Text;
using Document = Autodesk.Revit.DB.Document;


namespace RevitUtils
{
    public static class CollectorHelper
    {
        // Безопасная работа с коллектором

        /// <summary>
        /// Cобирает элементы из документа с применением фильтров
        /// </summary>
        public static (FilteredElementCollector, string) GetFilteredElementCollector(Document doc, params ElementFilter[] filters)
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
        /// Создает фильтр по конкретному параметру уровня
        /// </summary>
        public static ElementParameterFilter BuildNumericParameterFilter(BuiltInParameter bip, FilterNumericRuleEvaluator evaluator, ElementId levelId)
        {
            ParameterValueProvider valueProvider = new(new ElementId(bip));
            FilterElementIdRule filterRule = new(valueProvider, evaluator, levelId);
            return new ElementParameterFilter(filterRule);
        }


        #region FilteredByFamilylName

        public static List<FamilyInstance> GetInstancesByFamilyName(Document doc, BuiltInCategory bic, string familyName)
        {
            return [.. new FilteredElementCollector(doc).OfCategory(bic).OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType()
                .Cast<FamilyInstance>().Where(x => x.Symbol?.FamilyName?.IndexOf(familyName, StringComparison.OrdinalIgnoreCase) >= 0)];
        }

        #endregion


        #region FilteredBySymbolName
        public static FilteredElementCollector GetInstancesBySymbolName(Document doc, BuiltInCategory bic, string symbolName)
        {
            ElementId typeParamId = new(BuiltInParameter.ELEM_TYPE_PARAM);
            ElementId symbolParamId = new(BuiltInParameter.SYMBOL_NAME_PARAM);

            FilterRule typeRule = ParameterFilterRuleFactory.CreateEqualsRule(typeParamId, symbolName);
            FilterRule symbolRule = ParameterFilterRuleFactory.CreateEqualsRule(symbolParamId, symbolName);

            ElementParameterFilter typeFilter = new(typeRule);
            ElementParameterFilter symbolFilter = new(symbolRule);
            LogicalOrFilter logicOrFilter = new(typeFilter, symbolFilter);
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfCategory(bic);
            return collector.WherePasses(logicOrFilter).WhereElementIsNotElementType();
        }

        #endregion


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



        public static void DiagnoseCollectorIssues(Document doc, ElementFilter filter)
        {
            FilteredElementCollector collector = new(doc);

            int totalCount = collector.GetElementCount();
            Console.WriteLine($"Total elements: {totalCount}");

            // Затем применяем фильтр
            _ = collector.WherePasses(filter);
            int filteredCount = collector.GetElementCount();
            Console.WriteLine($"Filtered elements: {filteredCount}");

            if (filteredCount == 0 && totalCount > 0)
            {
                Console.WriteLine("⚠️ Фильтр исключил все элементы!");
            }
        }






    }
}
