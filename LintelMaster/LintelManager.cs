﻿using RevitUtils;
using System.Diagnostics;

namespace LintelMaster;

/// <summary>
/// Основной класс для маркировки перемычек
/// </summary>
public sealed class LintelManager(GroupingConfig config)
{
    //private readonly string _thickParam = config.ThickParameterName;
    //private readonly string _widthParam = config.WidthParameterName;
    //private readonly string _heightParam = config.HeightParameterName;

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
            (int thickRoundMm, int widthRoundMm, int heightRoundMm) = ExtractOpeningSize(instance);

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
    public (int Thick, int Width, int Height) ExtractOpeningSize(Element element)
    {
        double width = 0;
        double height = 0;

        if (element is FamilyInstance instance)
        {
            double thick = GetHostWallThickness(instance);

            int categoryId = instance.Category.Id.IntegerValue;

            if (categoryId == (int)BuiltInCategory.OST_Doors)
            {
                FamilySymbol doorSymbol = instance.Symbol;
                width = LintelUtils.GetParamValueDouble(doorSymbol, BuiltInParameter.DOOR_WIDTH);
                height = LintelUtils.GetParamValueDouble(doorSymbol, BuiltInParameter.DOOR_HEIGHT);
            }

            if (categoryId == (int)BuiltInCategory.OST_Windows)
            {
                FamilySymbol windowSymbol = instance.Symbol;
                width = LintelUtils.GetParamValueDouble(windowSymbol, BuiltInParameter.WINDOW_WIDTH);
                height = LintelUtils.GetParamValueDouble(windowSymbol, BuiltInParameter.WINDOW_HEIGHT);

            }

            int thickRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(thick));
            int widthRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(width, 50));
            int heightRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(height, 100));

            return (thickRoundMm, widthRoundMm, heightRoundMm);
        }

        throw new ArgumentException("Element is not a FamilyInstance");
    }

    /// <summary>
    /// Получает толщину стены-основы для элемента.
    /// </summary>
    /// <param name="instance">Экземпляр семейства (двери, окна и т.д.).</param>
    /// <returns>Толщина стены в миллиметрах или 0, если стена не найдена.</returns>
    public double GetHostWallThickness(FamilyInstance instance)
    {
        return instance?.Host is Wall hostWall ? hostWall.Width : 0;
    }



}