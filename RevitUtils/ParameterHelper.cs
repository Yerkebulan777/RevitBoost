namespace RevitUtils
{
    public static class ParameterHelper
    {
        /// <summary>
        /// Задает значение параметра
        /// </summary>
        public static bool SetParameterValue(Element element, string paramName, object value)
        {
            Parameter parameter = element?.LookupParameter(paramName);

            if (parameter is null || parameter.IsReadOnly)
            {
                throw new InvalidOperationException($"Параметр '{paramName}' не доступен!");
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
                        throw new InvalidOperationException($"Тип параметра '{parameter.StorageType}' не поддерживается.");
                }
            }

            return false;
        }
    }
}