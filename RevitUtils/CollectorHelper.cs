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

        public static IList<Category> GetFilterableCategories(Document doc, CategoryType ctype = CategoryType.Model, bool filterVisible = true)
        {
            List<Category> categories = new(50);

            foreach (ElementId catId in ParameterFilterUtilities.GetAllFilterableCategories())
            {
                Category cat = Category.GetCategory(doc, (BuiltInCategory)catId.IntegerValue);

                if (cat is not null && cat.CategoryType == ctype && cat.CanAddSubcategory)
                {
                    if (!filterVisible)
                    {
                        categories.Add(cat);
                    }
                    else if (cat.IsCuttable || cat.HasMaterialQuantities)
                    {
                        categories.Add(cat);
                    }
                }
            }

            return categories;
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
