using Autodesk.Revit.DB;
using System.Diagnostics;
using Document = Autodesk.Revit.DB.Document;


namespace RevitUtils
{
    public static class CollectorHelper
    {

        #region FilteredByFamilylName

        public static List<FamilyInstance> GetInstancesByFamilyName(Document doc, BuiltInCategory bic, string familyName)
        {
            return new FilteredElementCollector(doc).OfCategory(bic).OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType()
                .Cast<FamilyInstance>().Where(x => x.Symbol?.FamilyName?.IndexOf(familyName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
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


        public static FilteredElementCollector ExcludeElements(this FilteredElementCollector collector, ICollection<ElementId> elementIds)
        {
            return elementIds?.Count > 0 ? collector.WherePasses(new ExclusionFilter(elementIds)) : collector;
        }


        public static List<ElementId> GetModelCategoryIds(Document doc, List<BuiltInCategory> excluded = null)
        {
            List<ElementId> categoryIds = new(100);

            foreach (ElementId catId in ParameterFilterUtilities.GetAllFilterableCategories())
            {
                Category cat = Category.GetCategory(doc, catId);

                if (cat is not null && cat.CategoryType == CategoryType.Model)
                {
                    Debug.WriteLine($"Category: {cat.Name} Id: {cat.Id.IntegerValue}");

                    if (cat.CanAddSubcategory && cat.IsTagCategory && cat.IsVisibleInUI)
                    {
                        BuiltInCategory catBic = (BuiltInCategory)cat.Id.IntegerValue;

                        if (excluded is null || !excluded.Contains(catBic))
                        {
                            categoryIds.Add(catId);
                        }
                    }
                }
            }

            return categoryIds;
        }

        public static FilteredElementCollector GetAnnotationCollector(Document doc)
        {
            IList<BuiltInCategory> annotationCats = new[]
            {
                BuiltInCategory.OST_Lines,             // Линии детализации
                BuiltInCategory.OST_DetailComponents,  // Элементы детализации
                BuiltInCategory.OST_GenericAnnotation, // Условные обозначения
                BuiltInCategory.OST_FilledRegion,      // Заливки
                BuiltInCategory.OST_Dimensions         // Размеры
            };

            ElementMulticategoryFilter categoryFilter = new(annotationCats);
            FilteredElementCollector collector = new FilteredElementCollector(doc).WherePasses(categoryFilter);

            return collector;
        }

        public static FilteredElementCollector GetStructuraCollector(Document doc)
        {
            IList<BuiltInCategory> structuralBics = new[]
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_GenericModel,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralFoundation,
            };

            ElementMulticategoryFilter categoryFilter = new(structuralBics);
            FilteredElementCollector collector = new FilteredElementCollector(doc).WherePasses(categoryFilter);

            return collector;
        }

        public static FilteredElementCollector WhereHasParameterValue(this FilteredElementCollector collector, Parameter parameter)
        {
            HasValueFilterRule rule = new(parameter.Id);
            ElementParameterFilter filter = new(rule);
            return collector.WherePasses(filter);
        }

        public static FilteredElementCollector WhereSharedParameterApplicable(this FilteredElementCollector collector, string parameterName)
        {
            SharedParameterApplicableRule rule = new(parameterName);
            ElementParameterFilter filter = new(rule);
            return collector.WherePasses(filter);
        }

        /// <summary>
        ///   Retrieve ducts and pipes intersecting a given host.
        /// </summary>
        public static FilteredElementCollector GetMepClashes(HostObject host)
        {
            Document doc = host.Document;

            List<BuiltInCategory> cats =
            [
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_PipeCurves,
            ];

            ElementMulticategoryFilter mepfilter = new(cats);

            BoundingBoxXYZ bb = host.get_BoundingBox(null);

            BoundingBoxIsInsideFilter bbfilter = new(new Outline(bb.Min, bb.Max));

            FilteredElementCollector clashingElements
                = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .WherePasses(mepfilter)
                    .WherePasses(bbfilter);

            return clashingElements;
        }



    }
}
