using RevitUtils;
using System.Diagnostics;

namespace LintelMaster;

/// <summary>
/// Основной класс для маркировки перемычек
/// </summary>
public sealed class LintelManager(GroupingConfig config)
{
    private readonly string _thickParam = config.ThickParameterName;
    private readonly string _widthParam = config.WidthParameterName;
    private readonly string _heightParam = config.HeightParameterName;

    /// <summary>
    /// Категоризирует перемычки по их размерам
    /// </summary>
    public IDictionary<SizeKey, List<LintelData>> RetrieveLintelData(Document doc, string familyName)
    {
        HashSet<string> hostFamilies = [];

        SortedDictionary<SizeKey, List<LintelData>> result = [];

        BuiltInCategory bic = BuiltInCategory.OST_StructuralFraming;

        foreach (FamilyInstance instance in CollectorHelper.GetInstancesByFamilyName(doc, bic, familyName))
        {
            int thickRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(instance, _thickParam)));
            int widthRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(instance, _widthParam), 50));
            int heightRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(instance, _heightParam), 100));

            Debug.WriteLine($"Толщина: {thickRoundMm}, Ширина: {widthRoundMm}, Высота: {heightRoundMm}");

            FamilyInstance parentInstance = FamilyHelper.GetParentFamily(doc, instance);

            if (parentInstance != null)
            {
                hostFamilies.Add(parentInstance.Symbol.FamilyName);
            }

            LintelData lintelData = new(instance, thickRoundMm, widthRoundMm, heightRoundMm);

            if (!result.TryGetValue(lintelData.GroupKey, out List<LintelData> group))
            {
                result[lintelData.GroupKey] = group = [];
            }

            group.Add(lintelData);
        }

        Debug.WriteLine($"Host families: {string.Join(", ", hostFamilies)}");

        return result;
    }



}