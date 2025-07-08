using Autodesk.Revit.DB;
using CommonUtils;
using RevitUtils;
using System.Diagnostics;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LevelAssignment
{
    public sealed class FloorInfoGenerator
    {
        private readonly IModuleLogger _logger;
        private const int GROUND_NUMBER = 1; // Номер первого этажа
        private const int BASEMENT_NUMBER = -1; // Номер подземного этажа
        private const double LEVEL_MIN_HEIGHT = 1.5; // Минимальная высота этажа (м)
        private readonly int[] specialFloorNumbers = [99, 100, 101]; // Специальные номера этажей
        private static readonly Regex levelNumberRegex = new(@"^\d{1,3}.", RegexOptions.Compiled);

        public FloorInfoGenerator(IModuleLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Вычисляет модели этажей на основе уровней проекта
        /// </summary>
        public List<FloorInfo> GenerateFloorModels(Document doc)
        {
            _logger.Information("Starting floor models..." );

            List<FloorInfo> floorModels = [];

            List<Level> levels = GetSortedValidLevels(doc);

            if (levels.Count == 0)
            {
                _logger.Warning("No valid levels found!");
                return floorModels;
            }

            _logger.Debug("Valid levels {LevelCount}", levels.Count);
            Dictionary<int, Level> levelNumMap = CalculateLevelNumberData(levels);
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
        internal Dictionary<int, Level> CalculateLevelNumberData(List<Level> levels)
        {
            _logger.Debug("Calculating level numbers for {LevelCount} levels", levels.Count);

            int calculatedNumber = 0;
            double previousElevation = 0;
            Dictionary<int, Level> levelDictionary = [];

            List<Level> sortedLevels = [.. levels.OrderBy(x => x.Elevation)];

            for (int idx = 0; idx < sortedLevels.Count; idx++)
            {
                Level level = sortedLevels[idx];
                string levelName = level.Name.ToUpper();
                double elevation = GetProjectElevationInMeters(level);

                if (IsDuplicateLevel(elevation, previousElevation))
                {
                    _logger.Debug("Skipped duplicate level {LevelName}", levelName);
                    levelDictionary[calculatedNumber] = level;
                    continue;
                }

                _logger.Debug("Level {Index}: {Name} at {Elevat}", idx + 1, levelName, elevation);

                bool isHeightValid = Math.Abs(elevation - previousElevation) >= LEVEL_MIN_HEIGHT;
                bool isValidLevelNumber = IsValidFloorNumber(levelName, levels.Count, out int numberFromName);

                int oldNumber = calculatedNumber;

                if (isValidLevelNumber && isHeightValid && calculatedNumber <= numberFromName)
                {
                    calculatedNumber = numberFromName;
                    _logger.Debug("Used name number: {OldNumber} -> {NewNumber}", oldNumber, calculatedNumber);
                }
                else if (calculatedNumber <= 0 && elevation < -LEVEL_MIN_HEIGHT)
                {
                    calculatedNumber = BASEMENT_NUMBER;
                    _logger.Debug("Assigned basement: {OldNumber} -> {NewNumber}", oldNumber, calculatedNumber);
                }
                else if (calculatedNumber <= 0 && elevation < LEVEL_MIN_HEIGHT)
                {
                    calculatedNumber = GROUND_NUMBER;
                    _logger.Debug("Assigned ground floor: {OldNumber} -> {NewNumber}", oldNumber, calculatedNumber);
                }
                else if (IsTopLevel(calculatedNumber, idx, sortedLevels.Count))
                {
                    calculatedNumber = isHeightValid ? 100 : 101;

                    if (levelName.Contains("ЧЕРДАК"))
                    {
                        calculatedNumber = specialFloorNumbers[0]; // 99
                        _logger.Debug("Assigned attic: {NewNumber}", calculatedNumber);
                    }
                    else if (levelName.Contains("КРЫША"))
                    {
                        calculatedNumber = specialFloorNumbers[1]; // 100
                        _logger.Debug("Assigned roof: {NewNumber}", calculatedNumber);
                    }
                    else if (levelName.Contains("БУДКА"))
                    {
                        calculatedNumber = specialFloorNumbers[2]; // 101
                        _logger.Debug("Assigned penthouse: {NewNumber}", calculatedNumber);
                    }
                    else
                    {
                        _logger.Debug("Assigned top level: {OldNumber} -> {NewNumber}", oldNumber, calculatedNumber);
                    }
                }
                else if (calculatedNumber > 0 && isHeightValid)
                {
                    calculatedNumber += 1;
                    _logger.Debug("Incremented floor: {OldNumber} -> {NewNumber}", oldNumber, calculatedNumber);
                }
                else
                {
                    _logger.Debug("No change: floor remains {Number}", calculatedNumber);
                }

                levelDictionary[calculatedNumber] = level;
                previousElevation = elevation;

                _logger.Debug("Result: {LevelName} -> Floor {FloorNumber}", levelName, calculatedNumber);
            }

            _logger.Debug("Mapped {MappedCount} levels to floor numbers", levelDictionary.Count);
            return levelDictionary;
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
        private static double GetProjectElevationInMeters(Level level)
        {
            return Math.Round(UnitManager.FootToMm(level.ProjectElevation) / 1000.0, 3);
        }

        /// <summary>
        /// Проверяет, является ли уровень дублирующим (слишком близким по высоте)
        /// </summary>
        private static bool IsDuplicateLevel(double currentElevation, double previousElevation, double deviation = 1000)
        {
            return currentElevation > 0 && Math.Abs(currentElevation - previousElevation) < deviation;
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