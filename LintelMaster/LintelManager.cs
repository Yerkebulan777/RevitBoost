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
                _ = hostFamilies.Add(parentInstance.Symbol.FamilyName);
            }

            group.Add(lintelData);

        }

        Debug.WriteLine($"Host families: {string.Join(", ", hostFamilies)}");

        return result;
    }


    /// <summary>
    /// Извлекает размеры проемов из списка элементов (дверей и окон)
    /// </summary>
    /// <param name="elements">Список элементов проемов (двери, окна)</param>
    /// <returns>Словарь, где ключ - ID элемента, значение - размеры проема</returns>
    public Dictionary<ElementId, (int Thick, int Width, int Height)> ExtractOpeningSizes(List<Element> elements)
    {
        Dictionary<ElementId, (int Thick, int Width, int Height)> openingSizes = [];

        foreach (Element element in elements)
        {
            double thick = 0;
            double width = 0;
            double height = 0;

            if (element is FamilyInstance instance)
            {
                thick = GetHostWallThickness(instance);

                int categoryId = instance.Category.Id.IntegerValue;

                if (categoryId == (int)BuiltInCategory.OST_Doors)
                {
                    FamilySymbol doorSymbol = instance.Symbol;
                    width = LintelUtils.GetParamValueDouble(doorSymbol, BuiltInParameter.DOOR_WIDTH);
                    height = LintelUtils.GetParamValueDouble(doorSymbol, BuiltInParameter.DOOR_HEIGHT);
                }
                else if (categoryId == (int)BuiltInCategory.OST_Windows)
                {
                    FamilySymbol windowSymbol = instance.Symbol;
                    width = LintelUtils.GetParamValueDouble(windowSymbol, BuiltInParameter.WINDOW_WIDTH);
                    height = LintelUtils.GetParamValueDouble(windowSymbol, BuiltInParameter.WINDOW_HEIGHT);
                    
                }

                int thickRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(thick));
                int widthRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(width, 50));
                int heightRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(height, 100));

                openingSizes[element.Id] = (thickRoundMm, widthRoundMm, heightRoundMm);

            }
        }

        return openingSizes;
    }


    /// <summary>
    /// Получает толщину стены-основы для элемента.
    /// </summary>
    /// <param name="instance">Экземпляр семейства (двери, окна и т.д.).</param>
    /// <returns>Толщина стены в миллиметрах или 0, если стена не найдена.</returns>
    public double GetHostWallThickness(FamilyInstance instance)
    {
        if (instance?.Host is Wall hostWall)
        {
            return hostWall.Width;
        }

        return 0;
    }



}