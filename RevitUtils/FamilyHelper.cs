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
            return instance?.Host;
        }

        /// <summary>
        /// Находит родительское семейство по экземпляру вложенного семейства
        /// </summary>
        public static FamilyInstance GetParentFamily(FamilyInstance nestedInstance)
        {
            return nestedInstance?.SuperComponent as FamilyInstance;
        }

        /// <summary>
        /// Получает все вложенные семейства из семейства
        /// </summary>
        public static List<FamilyInstance> GetNestedFamilies(Document doc, FamilyInstance parent)
        {
            List<FamilyInstance> nestedInstances = [];

            try
            {
                foreach (ElementId id in parent.GetSubComponentIds())
                {
                    Element nestedElement = doc.GetElement(id);

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