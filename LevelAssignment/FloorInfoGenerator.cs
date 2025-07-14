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
        private const int TOP_OFFSET = 3; // Кол-во "верхниx" уровней
        private const double DEVIATION = 1000;  // Допустимое отклонение (мм)
        private const double MIN_HEIGHT = 1500; // Минимальная высота этажа (мм)
        private readonly int[] specialFloorNumbers = [99, 100, 101]; // Специальные номера этажей
        private static readonly Regex levelNumberRegex = new(@"^\d{1,3}.", RegexOptions.Compiled);
        private readonly IModuleLogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>
        /// Вычисляет модели этажей на основе уровней проекта
        /// </summary>
        public List<FloorInfo> GenerateFloorModels(Document doc)
        {
            List<Level> levels = CollectorHelper.GetSortedLevels(doc);

            List<FloorInfo> floorModels = [];

            if (levels.Count > 0)
            {
                Dictionary<int, Level> levelNumMap = CalculateFloorNumber(levels);

                foreach (IGrouping<int, Level> group in levelNumMap.GroupBy(kvp => kvp.Key, kvp => kvp.Value))
                {
                    List<Level> sortedLevels = [.. group.OrderBy(x => x.Elevation)];
                    floorModels.Add(new FloorInfo(group.Key, sortedLevels));
                }
            }

            return floorModels;
        }

        /// <summary>
        /// Вычисляет вычисляет номера уровней.
        /// </summary>
        internal Dictionary<int, Level> CalculateFloorNumber(List<Level> sortedLevels)
        {
            int currentNumber = 0;
            double previousElevation = 0;
            int levelTotalCount = sortedLevels.Count;

            Dictionary<int, Level> levelDictionary = [];

            _logger.Debug("CalculateFloorNumber: {Count} levels...", levelTotalCount);

            for (int idx = 0; idx < levelTotalCount; idx++)
            {
                Level level = sortedLevels[idx];

                double elevation = GetProjectElevationInMm(level);

                if (IsDuplicateLevel(elevation, previousElevation, out double difference))
                {
                    _logger.Warning("Skip duplicate level {LevelName}", level.Name);
                    levelDictionary[currentNumber] = level;
                    previousElevation = elevation;
                    continue;
                }

                LevelContext context = new()
                {
                    Index = idx,
                    LevelName = level.Name,
                    FloorNumber = currentNumber,
                    ElevationDelta = difference,
                    DisplayElevation = elevation,
                    TotalLevelCount = levelTotalCount,
                    PreviousElevation = previousElevation,
                };

                currentNumber = DetermineFloorNumber(in context);
                levelDictionary[currentNumber] = level;
                previousElevation = elevation;
            }

            _logger.Debug("Result: {Count} floor mappings", levelDictionary.Count);

            return levelDictionary;
        }

        /// <summary>
        /// Определение номера этажа на основе контекста уровня
        /// </summary>
        private int DetermineFloorNumber(in LevelContext context)
        {
            StringBuilder logBuilder = new();

            _ = logBuilder.AppendLine("DetermineFloorNumber...");
            _ = logBuilder.AppendLine($" ✓ Level name: {context.LevelName}");
            _ = logBuilder.AppendLine($" ✓ Display elevation: {context.DisplayElevation}");
            _ = logBuilder.AppendLine($" ✓ Index {context.Index} Delta = {context.ElevationDelta}");

            if (IsBasementLevel(in context, out int resultNumber))
            {
                _ = logBuilder.AppendLine($" ✓ Basement number → {resultNumber}");
            }
            else if (IsGroundLevel(in context, out resultNumber))
            {
                _ = logBuilder.AppendLine($" ✓ Ground level number → {resultNumber}");
            }
            else if (IsSpecialTopLevel(in context, out resultNumber))
            {
                _ = logBuilder.AppendLine($" ✓ Top level number → {resultNumber}");
            }
            else if (IsValidLevelName(in context, out resultNumber))
            {
                _ = logBuilder.AppendLine($" ✓ Valid level name → {resultNumber}");
            }
            else if (IsNextLevelAllowed(in context, out resultNumber))
            {
                _ = logBuilder.AppendLine($" ✓ Incremented level number → {resultNumber}");
            }
            else if (resultNumber == 0)
            {
                resultNumber = context.FloorNumber;
                _ = logBuilder.AppendLine($" ✗ UNCHANGED: number → {resultNumber}");
            }

            _ = logBuilder.AppendLine($" ▶ Number change {context.FloorNumber} ⇒ {resultNumber}");

            _logger.Debug(logBuilder.ToString());

            return resultNumber;
        }

        /// <summary>
        /// Получает высоту уровня в метрах
        /// </summary>
        private static double GetProjectElevationInMm(Level level)
        {
            return UnitManager.FootToMm(level.ProjectElevation);
        }

        /// <summary>
        /// Проверяет, является ли уровень дублирующим (слишком близким по высоте)
        /// </summary>
        private static bool IsDuplicateLevel(double current, double previous, out double difference)
        {
            difference = Math.Abs(current - previous);
            return current > 0 && difference < DEVIATION;
        }

        /// <summary>
        /// Проверяет, является ли уровень подземным уровнем
        /// </summary>
        private static bool IsBasementLevel(in LevelContext context, out int number)
        {
            number = 0;

            if (context.DisplayElevation < -MIN_HEIGHT)
            {
                number = -1;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Проверяет, является ли уровень первого этажа
        /// </summary>
        private static bool IsGroundLevel(in LevelContext context, out int number)
        {
            number = 0;

            if (context.FloorNumber <= 0 &&
                context.DisplayElevation > -MIN_HEIGHT &&
                context.DisplayElevation < MIN_HEIGHT)
            {
                number = 1;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Определяет, является ли уровень последним или предпоследним
        /// </summary>
        private static bool IsSpecialTopLevel(in LevelContext context, out int number)
        {
            number = 0;

            if (context.FloorNumber > TOP_OFFSET)
            {
                int topLevelOffsetIndex = context.TotalLevelCount - TOP_OFFSET;
                string levelNameInvariant = context.LevelName.ToUpperInvariant();

                if (levelNameInvariant.Contains("БУДКА"))
                {
                    number = 101;
                    return true;
                }
                if (levelNameInvariant.Contains("КРЫША"))
                {
                    number = 100;
                    return true;
                }
                if (levelNameInvariant.Contains("ЧЕРДАК"))
                {
                    number = 99;
                    return true;
                }
                if (context.Index > topLevelOffsetIndex)
                {
                    number = 100;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Проверяет валидность номера этажа из имени уровня
        /// </summary>
        private bool IsValidLevelName(in LevelContext context, out int number)
        {
            return TryParseNumber(context.LevelName, out number) && IsFloorNumberAllowed(in number, in context);
        }

        /// <summary>
        /// Проверяет, является ли изменение высоты уровня валидным
        /// </summary>
        private bool IsNextLevelAllowed(in LevelContext context, out int number)
        {
            number = 0;

            if (context.ElevationDelta >= MIN_HEIGHT)
            {
                int nextNumber = context.FloorNumber + 1;
                if (IsFloorNumberAllowed(in nextNumber, in context))
                {
                    number = nextNumber;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Извлекает число из имени уровня
        /// </summary>
        private bool TryParseNumber(string levelName, out int number)
        {
            Match match = levelNumberRegex.Match(levelName.Trim());

            if (match.Success && int.TryParse(match.Value, out number))
            {
                _logger.Debug("Extracted parsedLevelNumber {Number}", number);
                Debug.WriteLine($"Extracted parsedLevelNumber: {number}");
                return true;
            }

            number = 0;
            return false;
        }

        /// <summary>
        /// Проверяет является ли номер этажа валидным
        /// </summary>
        private bool IsFloorNumberAllowed(in int number, in LevelContext context)
        {
            if (number != 0 && number >= context.FloorNumber)
            {
                Debug.WriteLine($"Is floor number : {number} >= {context.FloorNumber}");
                return number < context.TotalLevelCount || specialFloorNumbers.Contains(number);
            }
            return false;
        }



    }
}