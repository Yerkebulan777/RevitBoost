using Autodesk.Revit.DB;
using System.Diagnostics;

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

        /// <summary>
        /// Получает числовое значение параметра по BuiltInParameter
        /// </summary>
        public static double GetParamValueAsDouble(Element element, BuiltInParameter paramId)
        {
            if (element is null || !element.IsValidObject)
            {
                Debug.Fail("Element is null or invalid");
                throw new ArgumentNullException(nameof(element), "Element cannot be null or invalid.");
            }

            if (paramId == BuiltInParameter.INVALID)
            {
                Debug.Fail("Invalid BuiltInParameter");
                throw new ArgumentException("BuiltInParameter cannot be INVALID.", nameof(paramId));
            }

            Parameter parameter = element.get_Parameter(paramId);

            return parameter?.HasValue == true && parameter.StorageType == StorageType.Double ? parameter.AsDouble() : 0;
        }

        /// <summary>
        /// Получает числовое значение параметра по имени
        /// </summary>
        public static double GetParamValueAsDouble(Element element, string paramName)
        {
            if (element is null || !element.IsValidObject)
            {
                Debug.Fail("Element is null or invalid");
                throw new ArgumentNullException(nameof(element), "Element cannot be null or invalid.");
            }

            if (string.IsNullOrWhiteSpace(paramName))
            {
                Debug.Fail("Parameter name is null or empty");
                throw new ArgumentException("Parameter name cannot be null or empty.", nameof(paramName));
            }

            Parameter parameter = element.LookupParameter(paramName);

            return parameter?.HasValue == true && parameter.StorageType == StorageType.Double ? parameter.AsDouble() : 0;
        }


    }
}