using RevitBIMTool.Models;
using RevitUtils;

namespace LintelMaster;

/// <summary>
/// Основной класс для маркировки перемычек
/// </summary>
public partial class LintelMarker
{
    private readonly Document _doc;
    private readonly MarkConfig _config;

    /// <summary>
    /// Создает экземпляр маркировщика с указанной конфигурацией
    /// </summary>
    /// <param name="doc">Документ Revit</param>
    /// <param name="config">Конфигурация маркировки</param>
    public LintelMarker(Document doc, MarkConfig config)
    {
        _config = config;
        _doc = doc;
    }

    /// <summary>
    /// Находит все перемычки в модели на основе наименования семейства
    /// </summary>
    /// <returns>Список перемычек</returns>
    public List<FamilyInstance> FindByFamilyName(string familyName)
    {
        BuiltInCategory bic = BuiltInCategory.OST_StructuralFraming;
        StringComparison comp = StringComparison.CurrentCultureIgnoreCase;

        IList<Element> instances = new FilteredElementCollector(_doc)
            .OfCategory(bic).OfClass(typeof(FamilyInstance))
            .ToElements();

        List<FamilyInstance> lintels = instances
            .OfType<FamilyInstance>()
            .Where(instance => instance.Symbol != null)
            .Where(instance => instance.Symbol.FamilyName.Equals(familyName, comp))
            .ToList();

        return lintels;
    }



}