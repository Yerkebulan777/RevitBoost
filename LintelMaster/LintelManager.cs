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
            int thickRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValueDouble(instance, _thickParam)));
            int widthRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValueDouble(instance, _widthParam), 50));
            int heightRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(LintelUtils.GetParamValueDouble(instance, _heightParam), 100));

            Debug.WriteLine($"Толщина: {thickRoundMm}, Ширина: {widthRoundMm}, Высота: {heightRoundMm}");

            LintelData lintelData = new(instance, thickRoundMm, widthRoundMm, heightRoundMm);

            if (!result.TryGetValue(lintelData.GroupKey, out List<LintelData> group))
            {
                result[lintelData.GroupKey] = group = [];
            }

            FamilyInstance parentInstance = FamilyHelper.GetParentFamily(doc, instance);

            if (parentInstance != null)
            {
                hostFamilies.Add(parentInstance.Symbol.FamilyName);
            }

            group.Add(lintelData);

        }

        Debug.WriteLine($"Host families: {string.Join(", ", hostFamilies)}");

        return result;
    }




    public void GetDoorWindowDimensions(Document doc)
    {
        // Сбор всех дверей
        var doors = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Doors)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>();

        // Сбор всех окон
        var windows = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Windows)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>();

        // Обработка дверей
        foreach (var door in doors)
        {
            double width = GetParameterValueDouble(door, BuiltInParameter.DOOR_WIDTH);
            double height = GetParameterValueDouble(door, BuiltInParameter.DOOR_HEIGHT);

            string sizeInfo = $"{door.Name}: Ширина={ConvertToMillimeters(width)} мм, Высота={ConvertToMillimeters(height)} мм";
            TaskDialog.Show("Размеры", sizeInfo);
        }

        // Обработка окон
        foreach (var window in windows)
        {
            var symbol = window.Symbol;
            double width = GetParameterValueDouble(symbol, BuiltInParameter.WINDOW_WIDTH);
            double height = GetParameterValueDouble(symbol, BuiltInParameter.WINDOW_HEIGHT);

            string sizeInfo = $"{window.Name}: Ширина={ConvertToMillimeters(width)} мм, Высота={ConvertToMillimeters(height)} мм";
            TaskDialog.Show("Размеры", sizeInfo);
        }
    }


    // Конвертация из футов в миллиметры
    private string ConvertToMillimeters(double feetValue)
    {
        return UnitUtils.ConvertFromInternalUnits(feetValue, UnitTypeId.Millimeters).ToString("F0");
    }


}