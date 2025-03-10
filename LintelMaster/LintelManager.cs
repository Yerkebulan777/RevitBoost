﻿using RevitUtils;

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
    public Dictionary<SizeKey, List<LintelData>> RetrieveLintelData(Document doc, string familyName)
    {
        Dictionary<SizeKey, List<LintelData>> result = new(100);

        BuiltInCategory bic = BuiltInCategory.OST_StructuralFraming;

        foreach (FamilyInstance lintel in CollectorHelper.GetInstancesByFamilyName(doc, bic, familyName))
        {
            int thickRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _thickParam)));
            int widthRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _widthParam), 50));
            int heightRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _heightParam), 100));

            LintelData lintelData = new(lintel, thickRoundMm, widthRoundMm, heightRoundMm);

            if (!result.TryGetValue(lintelData.GroupKey, out List<LintelData> group))
            {
                result[lintelData.GroupKey] = group = [];
            }

            group.Add(lintelData);
        }

        return result;
    }



}