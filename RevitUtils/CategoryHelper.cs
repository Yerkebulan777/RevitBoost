using Autodesk.Revit.DB;
using System.Diagnostics;
using Document = Autodesk.Revit.DB.Document;

namespace RevitUtils
{
    public static class CategoryHelper
    {
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

    }
}