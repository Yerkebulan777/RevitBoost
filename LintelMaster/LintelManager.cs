using RevitUtils;

namespace LintelMaster;

/// <summary>
/// Основной класс для маркировки перемычек
/// </summary>
public partial class LintelManager(Document doc, GroupingConfig config)
{
    private readonly string _thickParam = config.ThickParameter;
    private readonly string _widthParam = config.WidthParameter;
    private readonly string _heightParam = config.HeightParameter;

    /// <summary>
    /// Находит все семейства по наименованию
    /// </summary>
    /// <returns>Список перемычек</returns>
    public List<FamilyInstance> GetFamilyInstancesByName(string familyName)
    {
        BuiltInCategory bic = BuiltInCategory.OST_StructuralFraming;
        StringComparison comp = StringComparison.OrdinalIgnoreCase;

        List<FamilyInstance> lintels = new FilteredElementCollector(doc)
            .OfCategory(bic)
            .OfClass(typeof(FamilyInstance))
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .Where(instance => instance.Symbol?.FamilyName.Equals(familyName, comp) == true)
            .ToList();

        return lintels;
    }

    /// <summary>
    /// Категоризирует перемычки по их размерам
    /// </summary>
    public Dictionary<SizeKey, List<LintelData>> CategorizeLintelData(List<FamilyInstance> lintels)
    {
        // Предварительно рассчитываем ожидаемую ёмкость словаря
        Dictionary<SizeKey, List<LintelData>> result = new(Math.Min(lintels.Count, 50));

        foreach (FamilyInstance lintel in lintels)
        {
            // Получаем и округляем размеры (один вызов функции вместо трёх)
            int thickRound = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _thickParam)));
            int widthRound = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _widthParam), 50));
            int heightRound = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _heightParam), 150));

            SizeKey dimensions = new(thickRound, widthRound, heightRound);

            // Создаем объект данных перемычки
            LintelData lintelData = new(lintel)
            {
                Thick = thickRound,
                Width = widthRound,
                Height = heightRound,
                GroupKey = dimensions
            };

            // Более эффективно используем TryGetValue
            if (!result.TryGetValue(dimensions, out List<LintelData> group))
            {
                group = [];
                result[dimensions] = group;
            }

            group.Add(lintelData);
        }

        return result;
    }

}