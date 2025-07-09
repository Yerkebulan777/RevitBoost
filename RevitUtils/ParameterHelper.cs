using Autodesk.Revit.DB;

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
                        throw new InvalidOperationException($"{parameter.StorageType}");
                }
            }

            return false;
        }

        /// <summary>
        /// Получает числовое значение параметра по BuiltInParameter
        /// </summary>
        public static double GetParamValueAsDouble(Element element, BuiltInParameter paramId)
        {
            Parameter parameter = element.get_Parameter(paramId);

            return parameter.HasValue ? parameter.AsDouble() : 0;
        }

        /// <summary>
        /// Получает числовое значение параметра по имени
        /// </summary>
        public static double GetParamValueAsDouble(Element element, string paramName)
        {
            Parameter parameter = element.LookupParameter(paramName);
            return parameter.HasValue ? parameter.AsDouble() : 0;
        }



    }
}