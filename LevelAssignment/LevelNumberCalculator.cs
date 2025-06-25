using RevitUtils;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace LevelAssignment
{
    public sealed class LevelNumberCalculator
    {
        private const int GROUND_NUMBER = 1; // Номер первого этажа
        private const int BASEMENT_NUMBER = -1; // Номер подземного этажа
        private const double LEVEL_MIN_HEIGHT = 1.5; // Минимальная высота этажа (м)
        private readonly int[] specialFloorNumbers = [99, 100, 101]; // Специальные номера этажей
        private static readonly Regex levelNumberRegex = new(@"\d+", RegexOptions.Compiled);

        /// <summary>
        /// Вычисляет модели этажей на основе уровней проекта
        /// </summary>
        public List<FloorModel> GenerateFloorModels(List<Level> levels)
        {
            Dictionary<int, Level> levelNumMap = CalculateLevelNumberData(levels);

            List<FloorModel> floorModels = [];

            // Группируем уровни по номерам этажей
            foreach (IGrouping<int, Level> group in levelNumMap
                .GroupBy(kvp => kvp.Key, kvp => kvp.Value)
                .OrderBy(g => g.Min(level => level.Elevation)))
            {
                int floorNumber = group.Key;
                List<Level> floorLevels = [.. group]; // Уровни только этого этажа
                floorModels.Add(new FloorModel(floorNumber, floorLevels));
            }

            return floorModels;
        }


        /// <summary>
        /// Вычисляет вычисляет номера уровней.
        /// </summary>
        private Dictionary<int, Level> CalculateLevelNumberData(List<Level> levels)
        {
            int calculatedNumber = 0;
            double previousElevation = 0;

            Dictionary<int, Level> levelDictionary = [];

            Debug.WriteLine("Calculating level numbers...");

            List<Level> sortedLevels = [.. levels.OrderBy(x => x.Elevation)];

            for (int levelIdx = 0; levelIdx < sortedLevels.Count; levelIdx++)
            {
                Level level = sortedLevels[levelIdx];

                string levelName = level.Name.ToUpper();

                double elevation = GetProjectElevationInMeters(level);

                if (!IsDuplicateLevel(elevation, previousElevation))
                {
                    int numberFromName = ExtractNumberFromName(level.Name);
                    bool isValidLevelNumber = IsValidFloorNumber(numberFromName, levels.Count);
                    bool isHeightValid = Math.Abs(elevation - previousElevation) >= LEVEL_MIN_HEIGHT;

                    if (isValidLevelNumber && isHeightValid && calculatedNumber <= numberFromName)
                    {
                        calculatedNumber = numberFromName;
                    }

                    // если номер меньше или равно 0 и отметка ниже цоколя

                    else if (calculatedNumber <= 0 && elevation < -LEVEL_MIN_HEIGHT)
                    {
                        calculatedNumber = BASEMENT_NUMBER;
                    }

                    // если номер меньше или равно 0 и отметка выше цоколя

                    else if (calculatedNumber <= 0 && elevation < LEVEL_MIN_HEIGHT)
                    {
                        calculatedNumber = GROUND_NUMBER;
                    }

                    // если здание выше 3 этажей и уровень крыши или чердака

                    else if (IsTopLevel(calculatedNumber, levelIdx, sortedLevels.Count))
                    {
                        calculatedNumber = isHeightValid ? 100 : 101;

                        if (levelName.Contains("Чердак", StringComparison.OrdinalIgnoreCase))
                        {
                            calculatedNumber = specialFloorNumbers[0]; // 99
                        }
                        if (levelName.Contains("Крыша", StringComparison.OrdinalIgnoreCase))
                        {
                            calculatedNumber = specialFloorNumbers[1]; // 100
                        }
                        if (levelName.Contains("Будка", StringComparison.OrdinalIgnoreCase))
                        {
                            calculatedNumber = specialFloorNumbers[2]; // 101
                        }
                    }

                    // если уровень выше 1 этажа и высота валидна

                    else if (calculatedNumber > 0 && isHeightValid)
                    {
                        calculatedNumber += 1;
                    }
                }

                Debug.WriteLine($"Level: {levelName}, Elevation: {elevation} m");

                Debug.WriteLine($"Number: {calculatedNumber}");

                levelDictionary[calculatedNumber] = level;

                previousElevation = elevation;

            }

            return levelDictionary;
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
        /// <returns>true, если уровни слишком близки и один из них следует пропустить</returns>
        private static bool IsDuplicateLevel(double currentElevation, double previousElevation, double deviation = 1000)
        {
            return currentElevation > 0 && Math.Abs(currentElevation - previousElevation) < deviation;
        }


        /// <summary>
        /// Извлекает число из имени уровня
        /// </summary>
        private int ExtractNumberFromName(string levelName)
        {
            if (!string.IsNullOrEmpty(levelName))
            {
                Match match = levelNumberRegex.Match(levelName);
                return match.Success && int.TryParse(match.Value, out int number) ? number : 0;
            }

            return 0;
        }


        /// <summary>
        /// Определяет, является ли уровень последним или предпоследним
        /// </summary>
        private static bool IsTopLevel(int currentNum, int index, int lenght)
        {
            return currentNum > 3 && index > lenght - 3;
        }


        /// <summary>
        /// Проверяет валидность номера этажа из имени уровня
        /// </summary>
        private bool IsValidFloorNumber(int numberFromName, int totalLevels)
        {
            return numberFromName != 0 && (numberFromName < totalLevels || specialFloorNumbers.Contains(numberFromName));
        }


    }

}