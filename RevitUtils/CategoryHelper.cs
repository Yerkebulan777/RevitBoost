﻿using Autodesk.Revit.DB;
using System.Diagnostics;
using System.Text;
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

        public static (List<ElementId>, string) GetModelCategoryIds(Document doc)
        {
            StringBuilder builder = new();
            List<ElementId> categoryIds = new(100);

            builder.AppendLine($"🔍 Get model categories...");

            foreach (ElementId catId in ParameterFilterUtilities.GetAllFilterableCategories())
            {
                Category category = Category.GetCategory(doc, catId);

                if (category is not null  && category.CategoryType == CategoryType.Model)
                {
                    if (category.CanAddSubcategory && category.IsVisibleInUI)
                    {
                        builder.AppendLine($"✅ Model category: {category.Name}");
                        categoryIds.Add(catId);
                    }
                }
            }

            builder.AppendLine($"✅ Category Ids collected: {categoryIds.Count}\n");

            return (categoryIds, builder.ToString());
        }



    }
}