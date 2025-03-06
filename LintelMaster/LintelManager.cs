using RevitUtils;

namespace LintelMaster;

/// <summary>
/// Основной класс для маркировки перемычек
/// </summary>
public partial class LintelManager(Document doc, MarkingConfig config)
{
    private readonly string _thickParam = config.ThickParameter;
    private readonly string _widthParam = config.WidthParameter;
    private readonly string _heightParam = config.HeightParameter;

    private readonly Document _doc = doc;
    
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
            double thickRound = UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _thickParam));
            double widthRound = UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _widthParam), 50);
            double heightRound = UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _heightParam), 150);

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