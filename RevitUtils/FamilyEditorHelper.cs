namespace RevitUtils;

/// <summary>
/// Статический класс для работы с вложенными семействами в редакторе семейств
/// </summary>
public static class FamilyEditorHelper
{
    /// <summary>
    /// Находит родительское семейство по экземпляру вложенного семейства
    /// </summary>
    public static FamilyInstance GetParentFamily(Document doc, FamilyInstance nestedInstance)
    {
        Element parent = nestedInstance.SuperComponent;

        return parent is FamilyInstance instance ? instance : throw new Exception("Family does not have a super component!");
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