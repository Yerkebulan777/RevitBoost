using RevitUtils;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace LevelAssignment
{
    public sealed class FloorInfoGenerator
    {
        private const int GROUND_NUMBER = 1; // Номер первого этажа
        private const int BASEMENT_NUMBER = -1; // Номер подземного этажа
        private const double LEVEL_MIN_HEIGHT = 1.5; // Минимальная высота этажа (м)
        private readonly int[] specialFloorNumbers = [99, 100, 101]; // Специальные номера этажей
        private static readonly Regex levelNumberRegex = new(@"^\d{1,3}.", RegexOptions.Compiled);

        /// <summary>
        /// Вычисляет модели этажей на основе уровней проекта
        /// </summary>
        public List<FloorInfo> GenerateFloorModels(List<Level> levels)
        {
            List<FloorInfo> floorModels = [];

            // Получаем маппинг уровней на номера этажей
            Dictionary<int, Level> levelNumMap = CalculateLevelNumberData(levels);

            // Группируем уровни по номерам этажей
            foreach (IGrouping<int, Level> group in GroupLevelsByFloorNumber(levelNumMap))
            {
                int floorNumber = group.Key; // Номер этажа (ключ группы)
                List<Level> floorLevels = [.. group]; // Уровни только этого этажа
                floorModels.Add(new FloorInfo(floorNumber, floorLevels));
            }

            return floorModels;
        }

        /// <summary>
        /// Вычисляет вычисляет номера уровней.
        /// </summary>
        internal Dictionary<int, Level> CalculateLevelNumberData(List<Level> levels)
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
                    bool isHeightValid = Math.Abs(elevation - previousElevation) >= LEVEL_MIN_HEIGHT;
                    bool isValidLevelNumber = IsValidFloorNumber(level.Name, levels.Count, out int numberFromName);

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
        /// Проверяет валидность номера этажа из имени уровня
        /// </summary>
        internal bool IsValidFloorNumber(string levelName, int totalLevels, out int number)
        {
            bool hasNumber = TryParseNumber(levelName, out number);

            return hasNumber && (number < totalLevels || specialFloorNumbers.Contains(number));
        }

        /// <summary>
        /// Извлекает число из имени уровня
        /// </summary>
        private bool TryParseNumber(string levelName, out int number)
        {
            number = 0;

            Match match = levelNumberRegex.Match(levelName.Trim());

            return match.Success && int.TryParse(match.Value, out number);
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