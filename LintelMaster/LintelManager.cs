using RevitUtils;

namespace LintelMaster;

/// <summary>
/// Основной класс для маркировки перемычек
/// </summary>
public partial class LintelManager(GroupingConfig config)
{
    private readonly string _thickParam = config.ThickParameter;
    private readonly string _widthParam = config.WidthParameter;
    private readonly string _heightParam = config.HeightParameter;

    /// <summary>
    /// Находит все семейства по наименованию
    /// </summary>
    /// <returns>Список перемычек</returns>
    public List<FamilyInstance> GetInstancesByName(Document doc, string familyName)
    {
        BuiltInCategory bic = BuiltInCategory.OST_StructuralFraming;
        return CollectorHelper.GetInstancesByFamilyName(doc, bic, familyName);
    }

    /// <summary>
    /// Категоризирует перемычки по их размерам
    /// </summary>
    public Dictionary<SizeKey, List<LintelData>> CategorizeLintelData(List<FamilyInstance> lintels)
    {
        Dictionary<SizeKey, List<LintelData>> result = new(100);

        foreach (FamilyInstance lintel in lintels)
        {
            int thickRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _thickParam)));
            int widthRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _widthParam), 50));
            int heightRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _heightParam), 100));

            LintelData lintelData = new(lintel, thickRoundMm, widthRoundMm, heightRoundMm);
  
            if (!result.TryGetValue(lintelData.GroupKey, out List<LintelData> group))
            {
                result[lintelData.GroupKey] = group;
                group = [];
            }

            group.Add(lintelData);
        }

        return result;
    }

}