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

        public static List<ElementId> GetModelCategoryIds(Document doc, int lehght = 10)
        {
            List<ElementId> categoryIds = new(lehght);

            foreach (ElementId catId in ParameterFilterUtilities.GetAllFilterableCategories())
            {
                Category cat = Category.GetCategory(doc, catId);

                if (cat is not null && cat.CategoryType == CategoryType.Model)
                {
                    Debug.WriteLine($"Category: {cat.Name}, Id: {cat.Id.IntegerValue}");

                    if (cat.CanAddSubcategory && cat.IsTagCategory && cat.IsVisibleInUI)
                    {
                        categoryIds.Add(catId);
                    }
                }
            }

            return categoryIds;
        }


        public static FilteredElementCollector GetStructuraCollector(Document doc)
        {
            IList<BuiltInCategory> structuralCats = new[]
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_GenericModel,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralFoundation,
            };

            ElementMulticategoryFilter categoryFilter = new(structuralCats);
            FilteredElementCollector collector = new FilteredElementCollector(doc).WherePasses(categoryFilter);

            return collector;
        }

        #endregion


    }
}
