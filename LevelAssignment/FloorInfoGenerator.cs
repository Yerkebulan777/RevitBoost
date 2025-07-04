﻿using Autodesk.Revit.DB;
using CommonUtils;
using RevitUtils;
using System.Diagnostics;
using System.Text.RegularExpressions;

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
            List<FloorInfo> floorModels = [];

            List<Level> levels = GetSortedValidLevels(doc);

            Debug.WriteLine($"Total valid levels found: {levels.Count}");

            Dictionary<int, Level> levelNumMap = CalculateLevelNumberData(levels);

            foreach (IGrouping<int, Level> group in GroupLevelsByFloorNumber(levelNumMap))
            {
                int floorNumber = group.Key; // Номер этажа (ключ группы)
                List<Level> sortedLevels = [.. group.OrderBy(x => x.Elevation)];
                floorModels.Add(new FloorInfo(floorNumber, sortedLevels));
            }

            return floorModels;
        }

        /// <summary>
        /// Получает список уровней, которые имеют высоту меньше заданного максимума
        /// </summary>
        internal List<Level> GetSortedValidLevels(Document doc, double maxHeightInMeters = 100)
        {
            double maximum = UnitManager.MmToFoot(maxHeightInMeters * 1000);
            ParameterValueProvider provider = new(new ElementId(BuiltInParameter.LEVEL_ELEV));
            FilterDoubleRule rule = new(provider, new FilterNumericLess(), maximum, 5E-3);

            return [.. new FilteredElementCollector(doc).OfClass(typeof(Level))
                .WherePasses(new ElementParameterFilter(rule)).Cast<Level>()
                .OrderBy(x => x.Elevation)];
        }

        /// <summary>
        /// Вычисляет вычисляет номера уровней.
        /// </summary>
        internal Dictionary<int, Level> CalculateLevelNumberData(List<Level> levels)
        {
            _logger.LogDebug("Calculating level numbers for {LevelCount} levels", levels.Count);

            int calculatedNumber = 0;
            double previousElevation = 0;
            Dictionary<int, Level> levelDictionary = [];

            List<Level> sortedLevels = [.. levels.OrderBy(x => x.Elevation)];

            for (int levelIdx = 0; levelIdx < sortedLevels.Count; levelIdx++)
            {
                Level level = sortedLevels[levelIdx];
                string levelName = level.Name.ToUpper();
                double elevation = GetProjectElevationInMeters(level);

                _logger.LogDebug("Processing level {Index}: {LevelName} at {Elevation}m",
                    levelIdx + 1, levelName, elevation);

                if (IsDuplicateLevel(elevation, previousElevation))
                {
                    _logger.LogDebug("Skipped duplicate level {LevelName} (too close to previous)", levelName);
                    levelDictionary[calculatedNumber] = level;
                    continue;
                }

                bool isHeightValid = Math.Abs(elevation - previousElevation) >= LEVEL_MIN_HEIGHT;
                bool isValidLevelNumber = IsValidFloorNumber(level.Name, levels.Count, out int numberFromName);

                _logger.LogDebug("Height valid: {IsHeightValid}, Name number: {NameNumber}",
                    isHeightValid, isValidLevelNumber ? numberFromName.ToString() : "none");

                int oldNumber = calculatedNumber;

                if (isValidLevelNumber && isHeightValid && calculatedNumber <= numberFromName)
                {
                    calculatedNumber = numberFromName;
                    _logger.LogDebug("Used name number: {OldNumber} -> {NewNumber}", oldNumber, calculatedNumber);
                }
                else if (calculatedNumber <= 0 && elevation < -LEVEL_MIN_HEIGHT)
                {
                    calculatedNumber = BASEMENT_NUMBER;
                    _logger.LogDebug("Assigned basement: {OldNumber} -> {NewNumber}", oldNumber, calculatedNumber);
                }
                else if (calculatedNumber <= 0 && elevation < LEVEL_MIN_HEIGHT)
                {
                    calculatedNumber = GROUND_NUMBER;
                    _logger.LogDebug("Assigned ground floor: {OldNumber} -> {NewNumber}", oldNumber, calculatedNumber);
                }
                else if (IsTopLevel(calculatedNumber, levelIdx, sortedLevels.Count))
                {
                    calculatedNumber = isHeightValid ? 100 : 101;

                    if (levelName.Contains("ЧЕРДАК"))
                    {
                        calculatedNumber = specialFloorNumbers[0]; // 99
                        _logger.LogDebug("Assigned attic: {NewNumber}", calculatedNumber);
                    }
                    else if (levelName.Contains("КРЫША"))
                    {
                        calculatedNumber = specialFloorNumbers[1]; // 100
                        _logger.LogDebug("Assigned roof: {NewNumber}", calculatedNumber);
                    }
                    else if (levelName.Contains("БУДКА"))
                    {
                        calculatedNumber = specialFloorNumbers[2]; // 101
                        _logger.LogDebug("Assigned penthouse: {NewNumber}", calculatedNumber);
                    }
                    else
                    {
                        _logger.LogDebug("Assigned top level: {OldNumber} -> {NewNumber}", oldNumber, calculatedNumber);
                    }
                }
                else if (calculatedNumber > 0 && isHeightValid)
                {
                    calculatedNumber += 1;
                    _logger.LogDebug("Incremented floor: {OldNumber} -> {NewNumber}", oldNumber, calculatedNumber);
                }
                else
                {
                    _logger.LogDebug("No change: floor remains {Number}", calculatedNumber);
                }

                levelDictionary[calculatedNumber] = level;
                previousElevation = elevation;

                _logger.LogDebug("Result: {LevelName} -> Floor {FloorNumber}", levelName, calculatedNumber);
            }

            _logger.LogDebug("Mapped {MappedCount} levels to floor numbers", levelDictionary.Count);
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
                Debug.WriteLine($"Extracted number: {number}");
                return true;
            }

            number = 0;
            return false;
        }

        /// <summary>
        /// Группирует уровни по номерам этажей.
        /// </summary>
        private IEnumerable<IGrouping<int, Level>> GroupLevelsByFloorNumber(Dictionary<int, Level> data)
        {
            return data.GroupBy(kvp => kvp.Key, kvp => kvp.Value);
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