using Document = Autodesk.Revit.DB.Document;
using Level = Autodesk.Revit.DB.Level;


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

#if REVIT2019 || REVIT2021 || REVIT2022
            FilterRule typeRule = ParameterFilterRuleFactory.CreateEqualsRule(typeParamId, symbolName, false);
            FilterRule symbolRule = ParameterFilterRuleFactory.CreateEqualsRule(symbolParamId, symbolName, false);
#elif REVIT2023 || REVIT2024 || REVIT2025
            FilterRule typeRule = ParameterFilterRuleFactory.CreateEqualsRule(typeParamId, symbolName);
            FilterRule symbolRule = ParameterFilterRuleFactory.CreateEqualsRule(symbolParamId, symbolName);
#endif

            ElementParameterFilter typeFilter = new(typeRule);
            ElementParameterFilter symbolFilter = new(symbolRule);
            LogicalOrFilter logicOrFilter = new(typeFilter, symbolFilter);
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfCategory(bic);
            return collector.WherePasses(logicOrFilter).WhereElementIsNotElementType();
        }

        #endregion


        #region Category filter

        public static IList<Category> GetCategories(Document doc, CategoryType catType)
        {
            List<Category> categories = [];

            foreach (ElementId catId in ParameterFilterUtilities.GetAllFilterableCategories())
            {
                Category category = Category.GetCategory(doc, catId);

                if (category != null && category.CanAddSubcategory && category.CategoryType == catType)
                {
                    categories.Add(category);
                }
            }

            return categories;
        }

        #endregion


        #region Level filter

        public static List<Level> GetInValidLevels(Document doc, double maxHeightInMeters = 100)
        {
            double maximum = UnitManager.MmToFoot(maxHeightInMeters);
            ParameterValueProvider provider = new(new ElementId(BuiltInParameter.LEVEL_ELEV));
            FilterDoubleRule rule = new(provider, new FilterNumericGreaterOrEqual(), maximum, 5E-3);
            return new FilteredElementCollector(doc).OfClass(typeof(Level)).WherePasses(new ElementParameterFilter(rule))
                .Cast<Level>().OrderBy(x => x.ProjectElevation)
                .GroupBy(x => x.ProjectElevation)
                .Select(x => x.First())
                .ToList();
        }

        #endregion


    }
}
