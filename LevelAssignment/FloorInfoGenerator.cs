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
        private const double MIN_HEIGHT = 1500; // Минимальная высота этажа (мм)
        private const double DEVIATION = 1000;  // Максимальное допустимое отклонение (мм)
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

            for (int idx = 0; idx < levelTotalCount; idx++)
            {
                Level level = sortedLevels[idx];

                double elevation = GetProjectElevationInMm(level);

                if (IsDuplicateLevel(elevation, previousElevation, out double difference))
                {
                    _logger.Warning("Skip duplicate level {LevelName}", level.Name);
                    levelDictionary[currentNumber] = level;
                    continue;
                }

                LevelContext context = new()
                {
                    Index = idx,
                    LevelName = level.Name,
                    ElevationDelta = difference,
                    DisplayElevation = elevation,
                    CurrentNumber = currentNumber,
                    TotalLevelCount = levelTotalCount,
                    PreviousElevation = previousElevation,
                };

                currentNumber = DetermineFloorNumber(in context);

                levelDictionary[currentNumber] = level;
                previousElevation = context.DisplayElevation;
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

            int resultNumber = context.CurrentNumber;

            logBuilder.AppendLine("DetermineFloorNumber...");

            if (IsBasementLevel(in context))
            {
                resultNumber = BASEMENT_NUMBER;
                logBuilder.AppendLine($"  ✓ Basement → {resultNumber}");
            }
            else if (IsGroundLevel(in context))
            {
                resultNumber = GROUND_NUMBER;
                logBuilder.AppendLine($"  ✓ Ground level → {resultNumber}");
            }
            else if (IsTopLevel(in context))
            {
                resultNumber = GetTopNumber(in context);
                logBuilder.AppendLine($"  ✓ Top level → {resultNumber}");
            }
            else if (IsValidLevelName(in context, out int parsedLevelNumber))
            {
                resultNumber = parsedLevelNumber;
                logBuilder.AppendLine($"  ✓ Valid level name → {resultNumber}");
            }
            else if (IsValidElevationDelta(in context, out int nextFloorNumber))
            {
                resultNumber = nextFloorNumber;
                logBuilder.AppendLine($"  ✓ Incremented level nextFloorNumber → {resultNumber}");
            }
            else if (resultNumber == context.CurrentNumber)
            {
                logBuilder.AppendLine($"⚠️ UNCHANGED: {context.LevelName}!");
            }

            logBuilder.AppendLine($" ✓ Level name: {context.LevelName}");
            logBuilder.AppendLine($" ✓ Display elevation: {context.DisplayElevation}");
            logBuilder.AppendLine($" ✓ Index {context.Index} Delta = {context.ElevationDelta}");
            logBuilder.AppendLine($" ✓ Number change {context.CurrentNumber} → {resultNumber}");

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
        private static bool IsBasementLevel(in LevelContext context)
        {
            return context.CurrentNumber <= 0 && context.DisplayElevation < -MIN_HEIGHT;
        }

        /// <summary>
        /// Проверяет, является ли уровень первого этажа
        /// </summary>
        private static bool IsGroundLevel(in LevelContext context)
        {
            return context.CurrentNumber <= 0 && context.DisplayElevation < MIN_HEIGHT;
        }

        /// <summary>
        /// Определяет, является ли уровень последним или предпоследним
        /// </summary>
        private static bool IsTopLevel(in LevelContext context, int offset = 3)
        {
            return context.Index > (context.TotalLevelCount - offset) && context.CurrentNumber > offset;
        }

        /// <summary>
        /// Получает специальный номер этажа в зависимости от имени уровня
        /// </summary>
        private int GetTopNumber(in LevelContext context)
        {
            if (context.LevelName.Contains("ЧЕРДАК"))
            {
                return specialFloorNumbers[0]; // 99
            }

            if (context.LevelName.Contains("КРЫША"))
            {
                return specialFloorNumbers[1]; // 100
            }

            if (context.LevelName.Contains("БУДКА"))
            {
                return specialFloorNumbers[2]; // 101
            }

            return IsValidElevationDelta(in context, out int _) ? 100 : 101;
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
        private bool IsValidElevationDelta(in LevelContext context, out int nextFloorNumber)
        {
            nextFloorNumber = context.CurrentNumber + 1;
            return IsFloorNumberAllowed(in nextFloorNumber, in context) && context.ElevationDelta >= MIN_HEIGHT;
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
        private bool IsFloorNumberAllowed(in int levelNumber, in LevelContext context)
        {
            if (levelNumber != 0 && levelNumber >= context.CurrentNumber)
            {
                Debug.WriteLine($"Is floor number : {levelNumber} >= {context.CurrentNumber}");
                return levelNumber < context.TotalLevelCount || specialFloorNumbers.Contains(levelNumber);
            }
            return false;
        }



    }
}