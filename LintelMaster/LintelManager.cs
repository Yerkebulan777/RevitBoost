using Autodesk.Revit.DB;
using RevitUtils;

namespace LintelMaster
{
    /// <summary>
    /// Основной класс для маркировки перемычек (упрощенная версия)
    /// </summary>
    public sealed class LintelManager
    {
        private readonly GroupingConfig _config;

        public LintelManager(GroupingConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Категоризирует перемычки по их размерам
        /// </summary>
        public SortedDictionary<SizeKey, List<LintelData>> RetrieveLintelData(Document doc, string familyName)
        {
            if (doc?.IsValidObject != true)
            {
                throw new ArgumentException("Invalid document");
            }

            if (string.IsNullOrWhiteSpace(familyName))
            {
                throw new ArgumentException("Family name cannot be empty");
            }

            SortedDictionary<SizeKey, List<LintelData>> result = [];

            const BuiltInCategory bic = BuiltInCategory.OST_StructuralFraming;

            foreach (FamilyInstance instance in CollectorHelper.CollectInstancesBySymbolName(doc, bic, familyName).OfType<FamilyInstance>())
            {
                LintelData lintelData = ProcessSingleLintel(instance);

                if (lintelData != null)
                {
                    if (!result.TryGetValue(lintelData.GroupKey, out List<LintelData> group))
                    {
                        result[lintelData.GroupKey] = group = [];
                    }

                    group.Add(lintelData);
                }
            }

            return result;
        }

        /// <summary>
        /// Обрабатывает одну перемычку
        /// </summary>
        private LintelData ProcessSingleLintel(FamilyInstance instance)
        {
            if (instance?.IsValidObject != true)
            {
                return null;
            }

            Element parentInstance = FamilyHelper.GetParentFamily(instance);

            if (parentInstance == null)
            {
                return null;
            }

            (int thick, int width, int height)? dimensions = ExtractOpeningSize(parentInstance);

            if (dimensions == null)
            {
                return null;
            }

            (int thickMm, int widthMm, int heightMm) = dimensions.Value;

            return new LintelData(instance, thickMm, widthMm, heightMm);
        }

        /// <summary>
        /// Извлекает размеры проемов (возвращает null при ошибке)
        /// </summary>
        public (int thick, int width, int height)? ExtractOpeningSize(Element element)
        {
            if (element is not FamilyInstance instance)
            {
                return null;
            }

            try
            {
                double? thickness = GetHostWallThickness(instance);

                if (!thickness.HasValue)
                {
                    return null;
                }

                int categoryId = instance.Category?.Id?.IntegerValue ?? -1;

                (double width, double height)? dimensions = categoryId switch
                {
                    (int)BuiltInCategory.OST_Doors => ExtractDoorDimensions(instance),
                    (int)BuiltInCategory.OST_Windows => ExtractWindowDimensions(instance),
                    _ => null
                };

                if (!dimensions.HasValue)
                {
                    return null;
                }

                int thickMm = Convert.ToInt32(UnitManager.FootToMm(thickness.Value));
                int widthMm = Convert.ToInt32(UnitManager.FootToMm(dimensions.Value.width, 50));
                int heightMm = Convert.ToInt32(UnitManager.FootToMm(dimensions.Value.height, 100));

                return (thickMm, widthMm, heightMm);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Извлекает размеры дверей
        /// </summary>
        private (double width, double height)? ExtractDoorDimensions(FamilyInstance instance)
        {
            double width = ParameterHelper.GetValueAsDouble(instance.Symbol, BuiltInParameter.DOOR_WIDTH);
            if (width == 0)
            {
                width = ParameterHelper.GetValueAsDouble(instance, BuiltInParameter.DOOR_WIDTH);
            }

            double height = ParameterHelper.GetValueAsDouble(instance.Symbol, BuiltInParameter.DOOR_HEIGHT);
            if (height == 0)
            {
                height = ParameterHelper.GetValueAsDouble(instance, BuiltInParameter.DOOR_HEIGHT);
            }

            return width <= 0 || height <= 0 ? null : (width, height);
        }

        /// <summary>
        /// Извлекает размеры окон
        /// </summary>
        private (double width, double height)? ExtractWindowDimensions(FamilyInstance instance)
        {
            double width = ParameterHelper.GetValueAsDouble(instance.Symbol, BuiltInParameter.WINDOW_WIDTH);

            if (width == 0)
            {
                width = ParameterHelper.GetValueAsDouble(instance, BuiltInParameter.WINDOW_WIDTH);
            }

            double height = ParameterHelper.GetValueAsDouble(instance.Symbol, BuiltInParameter.WINDOW_HEIGHT);

            if (height == 0)
            {
                height = ParameterHelper.GetValueAsDouble(instance, BuiltInParameter.WINDOW_HEIGHT);
            }

            return width <= 0 || height <= 0 ? null : (width, height);
        }

        /// <summary>
        /// Получает толщину стены-основы (null при ошибке)
        /// </summary>
        public double? GetHostWallThickness(FamilyInstance instance)
        {
            return instance?.IsValidObject != true ? null : instance.Host is Wall hostWall && hostWall.Width > 0 ? hostWall.Width : (double?)null;
        }
    }
}