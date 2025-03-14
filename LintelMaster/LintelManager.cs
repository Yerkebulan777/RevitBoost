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
        SortedDictionary<SizeKey, List<LintelData>> result = [];

        BuiltInCategory bic = BuiltInCategory.OST_StructuralFraming;

        foreach (FamilyInstance instance in CollectorHelper.GetInstancesByFamilyName(doc, bic, familyName))
        {
            FamilyInstance parentInstance = FamilyHelper.GetParentFamily(instance);

            if (parentInstance is null)
            {
                Debug.WriteLine($"Family {instance.Name} instance does not have a valid host!");
                throw new ArgumentException("Family instance does not have a valid host!");
            }

            (int thickRoundMm, int widthRoundMm, int heightRoundMm) = ExtractOpeningSize(parentInstance);

            Debug.WriteLine($"Толщина: {thickRoundMm}, Ширина: {widthRoundMm}, Высота: {heightRoundMm}");

            LintelData lintelData = new(instance, thickRoundMm, widthRoundMm, heightRoundMm);

            if (!result.TryGetValue(lintelData.GroupKey, out List<LintelData> group))
            {
                result[lintelData.GroupKey] = group = [];
            }

            group.Add(lintelData);

        }

        return result;
    }


    /// <summary>
    /// Извлекает размеры проемов из списка элементов (дверей и окон)
    /// </summary>
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
                width = ParameterHelper.GetParamValueAsDouble(instance.Symbol, BuiltInParameter.DOOR_WIDTH);
                if (width == 0)
                {
                    width = ParameterHelper.GetParamValueAsDouble(instance, BuiltInParameter.DOOR_WIDTH);
                }

                height = ParameterHelper.GetParamValueAsDouble(instance.Symbol, BuiltInParameter.DOOR_HEIGHT);
                if (height == 0)
                {
                    height = ParameterHelper.GetParamValueAsDouble(instance, BuiltInParameter.DOOR_HEIGHT);
                }
            }

            if (categoryId == (int)BuiltInCategory.OST_Windows)
            {
                width = ParameterHelper.GetParamValueAsDouble(instance.Symbol, BuiltInParameter.WINDOW_WIDTH);
                if (width == 0)
                {
                    width = ParameterHelper.GetParamValueAsDouble(instance, BuiltInParameter.WINDOW_WIDTH);
                }

                height = ParameterHelper.GetParamValueAsDouble(instance.Symbol, BuiltInParameter.WINDOW_HEIGHT);
                if (height == 0)
                {
                    height = ParameterHelper.GetParamValueAsDouble(instance, BuiltInParameter.WINDOW_HEIGHT);
                }
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
    public double GetHostWallThickness(FamilyInstance instance)
    {
        return instance?.Host is Wall hostWall
            ? (double)hostWall.Width
            : throw new ArgumentException("Family instance does not have a valid host!");
    }



}