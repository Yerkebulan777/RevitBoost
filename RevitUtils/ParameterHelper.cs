namespace RevitUtils
{
    public static class ParameterHelper
    {
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

        /// <summary>
        /// Получает значение строкового параметра
        /// </summary>
        public static string GetParamValueString(Element element, string paramName)
        {
            Parameter prm = element.LookupParameter(paramName);

            if (prm != null && prm.HasValue)
            {
                if (prm.StorageType == StorageType.String)
                {
                    return prm.AsString();
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Получает значение целочисленного параметра
        /// </summary>
        public static int GetParamValueInteger(Element element, string paramName)
        {
            Parameter prm = element.LookupParameter(paramName);
            if (prm != null && prm.HasValue)
            {
                if (prm.StorageType == StorageType.Integer)
                {
                    return prm.AsInteger();
                }
            }
            return 0;
        }

        /// <summary>
        /// Получает значение целочисленного параметра
        /// </summary>
        public static int GetParamValueInteger(Element element, BuiltInParameter parameter)
        {
            Parameter param = element.get_Parameter(parameter);
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
        /// Получает значение ElementId параметра
        /// </summary>
        public static ElementId GetParamValueElementId(Element element, string paramName)
        {
            Parameter prm = element.LookupParameter(paramName);

            if (prm != null && prm.HasValue)
            {
                if (prm.StorageType == StorageType.ElementId)
                {
                    return prm.AsElementId();
                }
            }

            return ElementId.InvalidElementId;
        }

        /// <summary>
        /// Устанавливает значение параметра с автоматическим определением типа хранения
        /// </summary>
        public static bool SetParameterValue(Element element, string paramName, object value)
        {
            Parameter prm = element.LookupParameter(paramName);

            if (prm != null && !prm.IsReadOnly)
            {
                StorageType storageType = prm.StorageType;

                switch (storageType)
                {
                    case StorageType.Double:
                        if (value is double dblVal)
                        {
                            return prm.Set(dblVal);
                        }
                        else if (value != null && double.TryParse(value.ToString(), out double parsedDbl))
                        {
                            return prm.Set(parsedDbl);
                        }
                        break;

                    case StorageType.Integer:
                        if (value is int intVal)
                        {
                            return prm.Set(intVal);
                        }
                        else if (value != null && int.TryParse(value.ToString(), out int parsedInt))
                        {
                            return prm.Set(parsedInt);
                        }
                        break;

                    case StorageType.String:
                        if (value is string strVal)
                        {
                            return prm.Set(strVal);
                        }
                        else if (value != null)
                        {
                            return prm.Set(value.ToString());
                        }
                        break;

                    case StorageType.ElementId:
                        if (value is ElementId idVal)
                        {
                            return prm.Set(idVal);
                        }
                        break;
                }
            }

            return false;
        }

    }
}