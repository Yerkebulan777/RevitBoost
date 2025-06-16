using RevitUtils;
using System.Diagnostics;

namespace LintelMaster
{
    /// <summary>
    /// Основной класс для маркировки перемычек с улучшенной обработкой ошибок
    /// </summary>
    public sealed class LintelManager
    {
        private readonly GroupingConfig _config;

        public LintelManager(GroupingConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Категоризирует перемычки по их размерам с устойчивостью к ошибкам
        /// </summary>
        public LintelProcessingResult RetrieveLintelData(Document doc, string familyName)
        {
            if (doc?.IsValidObject != true)
            {
                return LintelProcessingResult.Failed("Invalid document");
            }

            if (string.IsNullOrWhiteSpace(familyName))
            {
                return LintelProcessingResult.Failed("Family name cannot be empty");
            }

            var result = new SortedDictionary<SizeKey, List<LintelData>>();
            var errors = new List<string>();
            var warnings = new List<string>();

            const BuiltInCategory bic = BuiltInCategory.OST_StructuralFraming;

            try
            {
                var instances = CollectorHelper.GetInstancesByFamilyName(doc, bic, familyName);

                if (instances.Count == 0)
                {
                    return LintelProcessingResult.Success(result, $"No instances found for family '{familyName}'");
                }

                int processedCount = 0;
                int skippedCount = 0;

                foreach (FamilyInstance instance in instances)
                {
                    try
                    {
                        var processingResult = ProcessSingleLintel(instance);

                        if (processingResult.IsSuccess)
                        {
                            LintelData lintelData = processingResult.LintelData;

                            if (!result.TryGetValue(lintelData.GroupKey, out List<LintelData> group))
                            {
                                result[lintelData.GroupKey] = group = new List<LintelData>();
                            }

                            group.Add(lintelData);
                            processedCount++;
                        }
                        else
                        {
                            skippedCount++;
                            warnings.Add($"Instance {instance.Id}: {processingResult.ErrorMessage}");

                            Debug.WriteLine($"Skipped lintel {instance.Id}: {processingResult.ErrorMessage}");
                        }
                    }
                    catch (Exception ex)
                    {
                        skippedCount++;
                        string errorMsg = $"Unexpected error processing instance {instance.Id}: {ex.Message}";
                        errors.Add(errorMsg);
                        Debug.WriteLine(errorMsg);
                    }
                }

                string summaryMessage = $"Processed: {processedCount}, Skipped: {skippedCount}, Groups: {result.Count}";

                if (errors.Count > 0)
                {
                    summaryMessage += $", Errors: {errors.Count}";
                }

                return LintelProcessingResult.Success(result, summaryMessage, warnings, errors);
            }
            catch (Exception ex)
            {
                string criticalError = $"Critical error during lintel data retrieval: {ex.Message}";
                Debug.WriteLine(criticalError);
                return LintelProcessingResult.Failed(criticalError);
            }
        }

        /// <summary>
        /// Обрабатывает одну перемычку с полной валидацией
        /// </summary>
        private LintelResult ProcessSingleLintel(FamilyInstance instance)
        {
            if (instance?.IsValidObject != true)
            {
                return LintelResult.Failed("Invalid family instance");
            }

            try
            {
                FamilyInstance parentInstance = FamilyHelper.GetParentFamily(instance);

                if (parentInstance == null)
                {
                    return LintelResult.Failed("No valid parent family found");
                }

                var extractionResult = ExtractOpeningSize(parentInstance);

                if (!extractionResult.IsSuccess)
                {
                    return LintelResult.Failed($"Failed to extract opening size: {extractionResult.ErrorMessage}");
                }

                var (thickRoundMm, widthRoundMm, heightRoundMm) = extractionResult.Dimensions;

                Debug.WriteLine($"Толщина: {thickRoundMm}, Ширина: {widthRoundMm}, Высота: {heightRoundMm}");

                LintelData lintelData = new(instance, thickRoundMm, widthRoundMm, heightRoundMm);

                return LintelResult.Success(lintelData);
            }
            catch (Exception ex)
            {
                return LintelResult.Failed($"Processing error: {ex.Message}");
            }
        }

        /// <summary>
        /// Извлекает размеры проемов с улучшенной валидацией
        /// </summary>
        public DimensionExtractionResult ExtractOpeningSize(Element element)
        {
            if (element?.IsValidObject != true)
            {
                return DimensionExtractionResult.Failed("Invalid element");
            }

            if (element is not FamilyInstance instance)
            {
                return DimensionExtractionResult.Failed("Element is not a FamilyInstance");
            }

            try
            {
                var thickResult = GetHostWallThickness(instance);
                if (!thickResult.IsSuccess)
                {
                    return DimensionExtractionResult.Failed($"Wall thickness error: {thickResult.ErrorMessage}");
                }

                double thick = thickResult.Thickness;

                int categoryId = instance.Category?.Id?.IntegerValue ?? -1;

                var dimensionResult = categoryId switch
                {
                    (int)BuiltInCategory.OST_Doors => ExtractDoorDimensions(instance),
                    (int)BuiltInCategory.OST_Windows => ExtractWindowDimensions(instance),
                    _ => DimensionResult.Failed($"Unsupported category: {categoryId}")
                };

                if (!dimensionResult.IsSuccess)
                {
                    return DimensionExtractionResult.Failed(dimensionResult.ErrorMessage);
                }

                int thickRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(thick));
                int widthRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(dimensionResult.Width, 50));
                int heightRoundMm = Convert.ToInt32(UnitManager.FootToRoundedMm(dimensionResult.Height, 100));

                return DimensionExtractionResult.Success((thickRoundMm, widthRoundMm, heightRoundMm));
            }
            catch (Exception ex)
            {
                return DimensionExtractionResult.Failed($"Extraction error: {ex.Message}");
            }
        }

        /// <summary>
        /// Извлекает размеры дверей
        /// </summary>
        private DimensionResult ExtractDoorDimensions(FamilyInstance instance)
        {
            double width = ParameterHelper.GetParamValueAsDouble(instance.Symbol, BuiltInParameter.DOOR_WIDTH);
            if (width == 0)
            {
                width = ParameterHelper.GetParamValueAsDouble(instance, BuiltInParameter.DOOR_WIDTH);
            }

            double height = ParameterHelper.GetParamValueAsDouble(instance.Symbol, BuiltInParameter.DOOR_HEIGHT);
            if (height == 0)
            {
                height = ParameterHelper.GetParamValueAsDouble(instance, BuiltInParameter.DOOR_HEIGHT);
            }

            if (width <= 0 || height <= 0)
            {
                return DimensionResult.Failed("Invalid door dimensions");
            }

            return DimensionResult.Success(width, height);
        }

        /// <summary>
        /// Извлекает размеры окон
        /// </summary>
        private DimensionResult ExtractWindowDimensions(FamilyInstance instance)
        {
            double width = ParameterHelper.GetParamValueAsDouble(instance.Symbol, BuiltInParameter.WINDOW_WIDTH);
            if (width == 0)
            {
                width = ParameterHelper.GetParamValueAsDouble(instance, BuiltInParameter.WINDOW_WIDTH);
            }

            double height = ParameterHelper.GetParamValueAsDouble(instance.Symbol, BuiltInParameter.WINDOW_HEIGHT);
            if (height == 0)
            {
                height = ParameterHelper.GetParamValueAsDouble(instance, BuiltInParameter.WINDOW_HEIGHT);
            }

            if (width <= 0 || height <= 0)
            {
                return DimensionResult.Failed("Invalid window dimensions");
            }

            return DimensionResult.Success(width, height);
        }

        /// <summary>
        /// Получает толщину стены-основы для элемента
        /// </summary>
        public ThicknessResult GetHostWallThickness(FamilyInstance instance)
        {
            if (instance?.IsValidObject != true)
            {
                return ThicknessResult.Failed("Invalid instance");
            }

            try
            {
                if (instance.Host is Wall hostWall)
                {
                    double thickness = hostWall.Width;

                    if (thickness > 0)
                    {
                        return ThicknessResult.Success(thickness);
                    }

                    return ThicknessResult.Failed("Wall thickness is zero or negative");
                }

                return ThicknessResult.Failed("Instance does not have a wall host");
            }
            catch (Exception ex)
            {
                return ThicknessResult.Failed($"Error getting wall thickness: {ex.Message}");
            }
        }


        #region Result Types

        public readonly struct LintelProcessingResult
        {
            public bool IsSuccess { get; }
            public IDictionary<SizeKey, List<LintelData>> Groups { get; }
            public string Message { get; }
            public IReadOnlyList<string> Warnings { get; }
            public IReadOnlyList<string> Errors { get; }

            private LintelProcessingResult(bool isSuccess, IDictionary<SizeKey, List<LintelData>> groups,
                string message, IReadOnlyList<string> warnings, IReadOnlyList<string> errors)
            {
                IsSuccess = isSuccess;
                Groups = groups ?? new Dictionary<SizeKey, List<LintelData>>();
                Message = message ?? string.Empty;
                Warnings = warnings ?? Array.Empty<string>();
                Errors = errors ?? Array.Empty<string>();
            }

            public static LintelProcessingResult Success(IDictionary<SizeKey, List<LintelData>> groups,
                string message = "Success", IReadOnlyList<string> warnings = null, IReadOnlyList<string> errors = null)
                => new(true, groups, message, warnings, errors);

            public static LintelProcessingResult Failed(string message)
                => new(false, null, message, null, null);
        }

        private readonly struct LintelResult
        {
            public bool IsSuccess { get; }
            public LintelData LintelData { get; }
            public string ErrorMessage { get; }

            private LintelResult(bool isSuccess, LintelData lintelData, string errorMessage)
            {
                IsSuccess = isSuccess;
                LintelData = lintelData;
                ErrorMessage = errorMessage ?? string.Empty;
            }

            public static LintelResult Success(LintelData lintelData) => new(true, lintelData, null);
            public static LintelResult Failed(string errorMessage) => new(false, default, errorMessage);
        }

        private readonly struct DimensionExtractionResult
        {
            public bool IsSuccess { get; }
            public (int Thick, int Width, int Height) Dimensions { get; }
            public string ErrorMessage { get; }

            private DimensionExtractionResult(bool isSuccess, (int, int, int) dimensions, string errorMessage)
            {
                IsSuccess = isSuccess;
                Dimensions = dimensions;
                ErrorMessage = errorMessage ?? string.Empty;
            }

            public static DimensionExtractionResult Success((int, int, int) dimensions)
                => new(true, dimensions, null);

            public static DimensionExtractionResult Failed(string errorMessage)
                => new(false, default, errorMessage);
        }

        private readonly struct DimensionResult
        {
            public bool IsSuccess { get; }
            public double Width { get; }
            public double Height { get; }
            public string ErrorMessage { get; }

            private DimensionResult(bool isSuccess, double width, double height, string errorMessage)
            {
                IsSuccess = isSuccess;
                Width = width;
                Height = height;
                ErrorMessage = errorMessage ?? string.Empty;
            }

            public static DimensionResult Success(double width, double height)
                => new(true, width, height, null);

            public static DimensionResult Failed(string errorMessage)
                => new(false, 0, 0, errorMessage);
        }

        private readonly struct ThicknessResult
        {
            public bool IsSuccess { get; }
            public double Thickness { get; }
            public string ErrorMessage { get; }

            private ThicknessResult(bool isSuccess, double thickness, string errorMessage)
            {
                IsSuccess = isSuccess;
                Thickness = thickness;
                ErrorMessage = errorMessage ?? string.Empty;
            }

            public static ThicknessResult Success(double thickness) => new(true, thickness, null);
            public static ThicknessResult Failed(string errorMessage) => new(false, 0, errorMessage);
        }

        #endregion


    }
}