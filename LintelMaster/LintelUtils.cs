namespace LintelMaster;

/// <summary>
/// Утилиты для работы с перемычками
/// </summary>
public static class LintelUtils
{
    /// <summary>
    /// Устанавливает наименование типа в системный параметр
    /// </summary>
    public static void SetTypeName(FamilyInstance lintel, string name)
    {
        // Получаем встроенный параметр для наименования типа
        Parameter param = lintel.Symbol.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME);

        // Устанавливаем значение, если параметр доступен для записи
        if (param != null && !param.IsReadOnly)
        {
            _ = param.Set(name);
        }
    }

    /// <summary>
    /// Устанавливает марку в общий параметр проекта
    /// </summary>
    public static void SetMark(FamilyInstance lintel, string paramName, string mark)
    {
        // Ищем проектный параметр BI_марка_изделия
        Parameter prm = lintel.LookupParameter(paramName);

        if (prm != null && !prm.IsReadOnly)
        {
            _ = prm.Set(mark);
        }
    }

    /// <summary>
    /// Получает значение числового параметра типа double
    /// </summary>
    public static double GetParamValueDouble(FamilyInstance instance, string paramName)
    {
        Parameter prm = instance.LookupParameter(paramName);

        if (prm != null && prm.HasValue)
        {
            if (prm.StorageType == StorageType.Double)
            {
                return prm.AsDouble();
            }
        }

        return 0;
    }

    /// <summary>
    /// Получает значение числового параметра типа double
    /// </summary>
    public static double GetParamValueDouble(Element element, BuiltInParameter parameter)
    {
        Parameter param = element.get_Parameter(parameter);

        if (param != null && param.HasValue)
        {
            if (param.StorageType == StorageType.Double)
            {
                return param.AsDouble();
            }
        }

        return 0;
    }


}
