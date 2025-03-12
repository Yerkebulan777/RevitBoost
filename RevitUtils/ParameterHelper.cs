namespace RevitUtils;

public static class ParameterHelper
{
    /// <summary>
    /// Получает значение числового параметра типа double
    /// </summary>
    public static double GetParamValueAsDouble(FamilyInstance instance, string paramName)
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
    public static double GetParamValueAsDouble(Element element, BuiltInParameter builtInParam)
    {
        Parameter param = element.get_Parameter(builtInParam);

        if (param != null && param.HasValue)
        {
            if (param.StorageType == StorageType.Double)
            {
                return param.AsDouble();
            }
        }
        return 0;
    }

    /// <summary>
    /// Получает значение строкового параметра
    /// </summary>
    public static string GetParamValueAsString(Element element, string paramName)
    {
        Parameter param = element.LookupParameter(paramName);

        if (param != null && param.HasValue)
        {
            if (param.StorageType == StorageType.String)
            {
                return param.AsString();
            }
        }

        return null;
    }

    /// <summary>
    /// Получает значение строкового параметра
    /// </summary>
    public static string GetParamValueAsString(Element element, BuiltInParameter builtInParam)
    {
        Parameter param = element.get_Parameter(builtInParam);

        if (param != null && param.HasValue)
        {
            if (param.StorageType == StorageType.String)
            {
                return param.AsString();
            }
        }

        return null;
    }

    /// <summary>
    /// Получает значение целочисленного параметра
    /// </summary>
    public static int GetParamValueAsInteger(Element element, string paramName)
    {
        Parameter param = element.LookupParameter(paramName);

        if (param != null && param.HasValue)
        {
            if (param.StorageType == StorageType.Integer)
            {
                return param.AsInteger();
            }
        }

        return 0;
    }

    /// <summary>
    /// Получает значение целочисленного параметра
    /// </summary>
    public static int GetParamValueAsInteger(Element element, BuiltInParameter builtInParam)
    {
        Parameter param = element.get_Parameter(builtInParam);

        if (param != null && param.HasValue)
        {
            if (param.StorageType == StorageType.Integer)
            {
                return param.AsInteger();
            }
        }

        return 0;
    }

    /// <summary>
    /// Задает значение параметра
    /// </summary>
    public static bool SetParameterValue(Element element, string paramName, object value)
    {
        Parameter parameter = element?.LookupParameter(paramName);

        if (parameter is null || parameter.IsReadOnly)
        {
            throw new Exception($"Параметр не доступен!");
        }

        if (value is not null)
        {
            switch (parameter.StorageType)
            {
                case StorageType.Double:

                    if (value is double doubleVal)
                    {
                        return parameter.Set(doubleVal);
                    }
                    return parameter.Set(Convert.ToDouble(value));

                case StorageType.Integer:

                    if (value is int intVal)
                    {
                        return parameter.Set(intVal);
                    }
                    return parameter.Set(Convert.ToInt32(value));

                case StorageType.String:

                    if (value is string strVal)
                    {
                        return parameter.Set(strVal);
                    }
                    return parameter.Set(Convert.ToString(value));

                case StorageType.ElementId:

                    if (value is ElementId idVal)
                    {
                        return parameter.Set(idVal);
                    }
                    return parameter.Set(new ElementId(Convert.ToInt32(value)));

                default:
                    throw new Exception($"Тип параметра {parameter.StorageType}");
            }
        }

        return false;
    }
}