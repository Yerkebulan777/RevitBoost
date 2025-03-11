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
        HashSet<string> familyNames = [];

        SortedDictionary<SizeKey, List<LintelData>> result = [];

        BuiltInCategory bic = BuiltInCategory.OST_StructuralFraming;

        foreach (FamilyInstance lintel in CollectorHelper.GetInstancesByFamilyName(doc, bic, familyName))
        {
            int thickRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _thickParam)));
            int widthRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _widthParam), 50));
            int heightRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _heightParam), 100));

            Debug.WriteLine($"Толщина: {thickRoundMm}, Ширина: {widthRoundMm}, Высота: {heightRoundMm}");

            LintelData lintelData = new(lintel, thickRoundMm, widthRoundMm, heightRoundMm);

            if (!result.TryGetValue(lintelData.GroupKey, out List<LintelData> group))
            {
                result[lintelData.GroupKey] = group = [];
            }

            familyNames.Add(lintel.Symbol.FamilyName);

            group.Add(lintelData);
        }

        return result;
    }



}