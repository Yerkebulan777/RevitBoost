using System.Diagnostics;

namespace RevitUtils;

/// <summary>
/// Статический класс для работы с вложенными семействами в редакторе семейств
/// </summary>
public static class FamilyHelper
{
    /// <summary>
    /// Получает элемент-основу, в котором размещен экземпляр семейства
    /// </summary>
    /// <param name="instance">Экземпляр семейства</param>
    /// <returns>Элемент-основа или null, если основа отсутствует</returns>
    public static Element GetHost(FamilyInstance instance)
    {
        Element host = instance?.Host;

        if (host is null || !host.IsValidObject)
        {
            Debug.WriteLine("Family instance does not have a valid host!");
            throw new ArgumentException("Family instance does not have a valid host!");
        }

        return host;
    }

    /// <summary>
    /// Находит родительское семейство по экземпляру вложенного семейства
    /// </summary>
    public static FamilyInstance GetParentFamily(FamilyInstance nestedInstance)
    {
        Element parent = nestedInstance.SuperComponent;

        if (parent is FamilyInstance instance)
        {
            return instance;
        }

        Debug.WriteLine($"Family does not have a valid super component");
        throw new ArgumentException("Family does not have a super component!");
    }

    /// <summary>
    /// Получает все вложенные семейства из семейства
    /// </summary>
    public static List<FamilyInstance> GetNestedFamilies(Document doc, FamilyInstance parentInstance)
    {
        List<FamilyInstance> nestedInstances = [];

        foreach (ElementId id in parentInstance.GetSubComponentIds())
        {
            Element nestedElement = doc.GetElement(id);

            if (nestedElement is FamilyInstance nested)
            {
                nestedInstances.Add(nested);
            }
        }

        return nestedInstances;
    }



}