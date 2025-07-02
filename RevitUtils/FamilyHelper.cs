using Autodesk.Revit.DB;
using System.Diagnostics;


namespace RevitUtils
{
    /// <summary>
    /// Статический класс для работы с вложенными семействами в редакторе семейств
    /// </summary>
    public static class FamilyHelper
    {
        /// <summary>
        /// Получает элемент-основу, в котором размещен экземпляр семейства
        /// </summary>
        /// <returns>Элемент-основа или null, если основа отсутствует</returns>
        public static Element GetHost(FamilyInstance instance)
        {
            if (instance is null || !instance.IsValidObject)
            {
                Debug.WriteLine("Instance is null or invalid");
            }

            Element host = instance?.Host;

            return host is Instance && host.IsValidObject ? host : null;
        }

        /// <summary>
        /// Находит родительское семейство по экземпляру вложенного семейства
        /// </summary>
        public static FamilyInstance GetParentFamily(FamilyInstance nestedInstance)
        {
            if (nestedInstance is null || !nestedInstance.IsValidObject)
            {
                Debug.WriteLine("Nested instance is null or invalid");
                return null;
            }

            try
            {
                Element parent = nestedInstance.SuperComponent;

                if (parent is FamilyInstance parentInstance && parentInstance.IsValidObject)
                {
                    return parentInstance;
                }

                Debug.WriteLine("Family does not have a valid super component");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting parent family: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Получает все вложенные семейства из семейства
        /// </summary>
        public static List<FamilyInstance> GetNestedFamilies(Document doc, FamilyInstance parent)
        {
            if (doc is null || parent is null || !parent.IsValidObject)
            {
                Debug.WriteLine("Document or parent instance is null or invalid");
                return [];
            }

            List<FamilyInstance> nestedInstances = [];

            try
            {
                foreach (ElementId id in parent.GetSubComponentIds())
                {
                    Element? nestedElement = doc.GetElement(id);

                    if (nestedElement is FamilyInstance nested && nested.IsValidObject)
                    {
                        nestedInstances.Add(nested);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting nested families: {ex.Message}");
            }

            return nestedInstances;
        }



    }
}