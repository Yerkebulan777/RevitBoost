using Autodesk.Revit.DB;
using CommonUtils;
using RevitUtils;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace LevelAssignment
{
    public sealed class FloorInfoGenerator(IModuleLogger logger)
    {
        private const int GROUND_NUMBER = 1; // Номер первого этажа
        private const int BASEMENT_NUMBER = -1; // Номер подземного этажа
        private const int MIN_HEIGHT = 1500; // Минимальная высота этажа (мм)
        private const int DEVIATION = 1000; // Максимальное допустимое отклонение (мм)
        private readonly int[] specialFloorNumbers = [99, 100, 101]; // Специальные номера этажей
        private static readonly Regex levelNumberRegex = new(@"^\d{1,3}.", RegexOptions.Compiled);
        private readonly IModuleLogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>
        /// Вычисляет модели этажей на основе уровней проекта
        /// </summary>
        public List<FloorInfo> GenerateFloorModels(Document doc)
        {
            _logger.Information("Starting floor models...");

            List<FloorInfo> floorModels = [];

            List<Level> levels = GetSortedValidLevels(doc);

            if (levels.Count == 0)
            {
                _logger.Warning("No valid levels found!");
                return floorModels;
            }

            Dictionary<int, Level> levelNumMap = CalculateFloorNumber(levels);

            _logger.Debug("Mapped {MappedLevels} levels to floor numbers", levelNumMap.Count);

            foreach (IGrouping<int, Level> group in levelNumMap.GroupBy(kvp => kvp.Key, kvp => kvp.Value))
            {
                List<Level> sortedLevels = [.. group.OrderBy(x => x.Elevation)];

                _logger.Debug("Floor model {FloorNumber} with {LevelCount} levels", group.Key, sortedLevels.Count);

                floorModels.Add(new FloorInfo(group.Key, sortedLevels));
            }

            _logger.Information("Generated {FloorModelCount} floor models successfully", floorModels.Count);

            return floorModels;
        }

        /// <summary>
        /// Получает список уровней, которые имеют высоту меньше заданного максимума
        /// </summary>
        internal static List<Level> GetSortedValidLevels(Document doc, double maxHeigh = 100)
        {
            FilterNumericLess evaluator = new();
            ParameterValueProvider provider = new(new ElementId(BuiltInParameter.LEVEL_ELEV));
            double maximum = UnitManager.MmToFoot((maxHeigh * 1000) - 1000);
            FilterDoubleRule rule = new(provider, evaluator, maximum, 5E-3);

            return [.. new FilteredElementCollector(doc).OfClass(typeof(Level))
                .WherePasses(new ElementParameterFilter(rule)).Cast<Level>()
                .OrderBy(x => x.Elevation)];
        }

        /// <summary>
        /// Вычисляет вычисляет номера уровней.
        /// </summary>
        internal Dictionary<int, Level> CalculateFloorNumber(List<Level> sortedLevels)
        {
            int calculatedNumber = 0;
            double previousElevation = 0;
            int levelTotalCount = sortedLevels.Count;

            Dictionary<int, Level> levelDictionary = [];

            _logger.Debug("Start: {LevelCount} levels", levelTotalCount);

            for (int idx = 0; idx < levelTotalCount; idx++)
            {
                Level level = sortedLevels[idx];

                double elevation = GetProjectElevationInMm(level);

                if (IsDuplicateLevel(elevation, previousElevation, out double difference))
                {
                    _logger.Debug("→ Skip: duplicate height (diff={Diff:F2}m)", difference);
                    levelDictionary[calculatedNumber] = level;
                    continue;
                }

                LevelContext context = new()
                {
                    Index = idx,
                    LevelName = level.Name,
                    DisplayElevation = elevation,
                    TotalLevels = levelTotalCount,
                    ElevationDifference = difference,
                    PreviousElevation = previousElevation,
                };

                _logger.Debug("DisplayElevation difference = {Diff:F2}", difference);
                calculatedNumber = DetermineFloorNumber(calculatedNumber, context);

                levelDictionary[calculatedNumber] = level;
                previousElevation = context.DisplayElevation;
            }

            _logger.Debug("Result: {Count} floor mappings", levelDictionary.Count);
            return levelDictionary;
        }

        /// <summary>
        /// Определение номера этажа на основе контекста уровня
        /// </summary>
        private int DetermineFloorNumber(int currentNumber, LevelContext context)
        {
            StringBuilder logBuilder = new();

            int resultNumber = currentNumber;

            bool isGround = context.DisplayElevation < MIN_HEIGHT;
            bool isBasement = context.DisplayElevation < -MIN_HEIGHT;
            bool isHeightValid = context.ElevationDifference >= MIN_HEIGHT;
            bool isTopLevel = IsTopLevel(currentNumber, context.Index, context.TotalLevels);
            bool isValidName = IsValidFloorNumber(context.LevelName, context.TotalLevels, out int numFromName);

            logBuilder.AppendLine($"Level '{context.LevelName}' at {context.DisplayElevation:F2}m");
            logBuilder.AppendLine($"  heightOK={isHeightValid} (diff>={MIN_HEIGHT})");
            logBuilder.AppendLine($"  curr={currentNumber}, diff={context.ElevationDifference:F2}");
            logBuilder.AppendLine($"  topLevel={isTopLevel} ({context.Index}/{context.TotalLevels})");
            logBuilder.AppendLine($"  basement={isBasement}, ground={isGround}");
            logBuilder.AppendLine($"  nameOK={isValidName} (num={numFromName})");

            if (isValidName && isHeightValid && currentNumber <= numFromName)
            {
                resultNumber = numFromName;
                logBuilder.AppendLine($"  ✓ nameOK && heightOK && curr<=num → {resultNumber}");
            }
            else if (currentNumber <= 0 && isBasement)
            {
                resultNumber = BASEMENT_NUMBER;
                logBuilder.AppendLine($"  ✓ curr<=0 && basement → {resultNumber}");
            }
            else if (currentNumber <= 0 && isGround)
            {
                resultNumber = GROUND_NUMBER;
                logBuilder.AppendLine($"  ✓ curr<=0 && ground → {resultNumber}");
            }
            else if (isTopLevel)
            {
                resultNumber = GetSpecialFloorNumber(context.LevelName, isHeightValid);
                logBuilder.AppendLine($"  ✓ topLevel → {resultNumber}");
            }
            else if (currentNumber > 0 && isHeightValid)
            {
                resultNumber = currentNumber + 1;
                logBuilder.AppendLine($"  ✓ curr>0 && heightOK → {resultNumber}");
            }

            if (resultNumber == currentNumber)
            {
                logBuilder.AppendLine($"⚠️ UNCHANGED: {currentNumber}!");
            }

            _logger.Debug(logBuilder.ToString());

            return resultNumber;
        }

        /// <summary>
        /// Получает специальный номер этажа в зависимости от имени уровня
        /// </summary>
        private int GetSpecialFloorNumber(string levelName, bool isHeightValid)
        {
            if (levelName.Contains("ЧЕРДАК"))
            {
                return specialFloorNumbers[0]; // 99
            }

            if (levelName.Contains("КРЫША"))
            {
                return specialFloorNumbers[1]; // 100
            }

            if (levelName.Contains("БУДКА"))
            {
                return specialFloorNumbers[2]; // 101
            }

            return isHeightValid ? 100 : 101;
        }

        /// <summary>
        /// Проверяет валидность номера этажа из имени уровня
        /// </summary>
        internal bool IsValidFloorNumber(string levelName, int totalLevels, out int number)
        {
            return TryParseNumber(levelName, out number) && (number < totalLevels || specialFloorNumbers.Contains(number));
        }

        /// <summary>
        /// Извлекает число из имени уровня
        /// </summary>
        private bool TryParseNumber(string levelName, out int number)
        {
            Match match = levelNumberRegex.Match(levelName.Trim());

            if (match.Success && int.TryParse(match.Value, out number))
            {
                _logger.Debug("Extracted number {Number}", number);
                Debug.WriteLine($"Extracted number: {number}");
                return true;
            }

            number = 0;
            return false;
        }

        /// <summary>
        /// Преобразует высоту уровня в метры.
        /// </summary>
        private static double GetProjectElevationInMm(Level level)
        {
            return UnitManager.FootToMm(level.ProjectElevation);
        }

        /// <summary>
        /// Проверяет, является ли уровень дублирующим (слишком близким по высоте)
        /// </summary>
        private static bool IsDuplicateLevel(double currentElevation, double previousElevation, out double difference)
        {
            difference = Math.Abs(currentElevation - previousElevation);
            return currentElevation > 0 && difference < DEVIATION;
        }

        /// <summary>
        /// Определяет, является ли уровень последним или предпоследним
        /// </summary>
        private static bool IsTopLevel(int currentNum, int index, int lenght)
        {
            return currentNum > 3 && index > lenght - 3;
        }



    }
}