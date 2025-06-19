using RevitUtils;
using System.Text.RegularExpressions;

namespace LevelAssignment
{
    public sealed class LevelNumberCalculator
    {
        private const int GROUND_NUMBER = 1; // Номер первого этажа
        private const int BASEMENT_NUMBER = -1; // Номер подземного этажа
        private const double LEVEL_MIN_HEIGHT = 1.8; // Минимальная высота этажа (м)
        private readonly int[] specialFloorNumbers = [99, 100, 101]; // Специальные номера этажей

        private static readonly Regex levelNumberRegex = new(@"\d+", RegexOptions.Compiled);


        public List<Level> GetValidLevels(Document doc, double maxHeightInMeters = 100)
        {
            double maximum = UnitManager.MmToFoot(maxHeightInMeters * 1000);
            ParameterValueProvider provider = new(new ElementId(BuiltInParameter.LEVEL_ELEV));
            FilterDoubleRule rule = new(provider, new FilterNumericLess(), maximum, 5E-3);

            return [.. new FilteredElementCollector(doc).OfClass(typeof(Level))
                .WherePasses(new ElementParameterFilter(rule)).Cast<Level>()
                .OrderBy(x => x.Elevation).GroupBy(x => x.Elevation)
                .Select(x => x.First())];
        }


        public Dictionary<int, Level> CalculateLevelNumberData(List<Level> levels)
        {
            double previousElevation = 0;
            int calculatedFloorNumber = 0;

            Dictionary<int, Level> levelDictionary = [];

            List<Level> sortedLevels = [.. levels.OrderBy(x => x.Elevation)];

            for (int levelIndex = 0; levelIndex < sortedLevels.Count; levelIndex++)
            {
                Level level = sortedLevels[levelIndex];

                double elevation = GetElevationInMeters(level);

                if (!IsDuplicateLevel(elevation, previousElevation))
                {
                    int numberFromName = ExtractNumberFromName(level.Name);
                    bool isValidLevelNumber = IsValidFloorNumber(numberFromName, levels.Count);
                    bool isHeightValid = Math.Abs(elevation - previousElevation) >= LEVEL_MIN_HEIGHT;
                    bool isLastOrSecondLastLevel = IsLastOrSecondLastLevel(levelIndex, sortedLevels.Count);

                    if (isValidLevelNumber && isHeightValid && calculatedFloorNumber <= numberFromName)
                    {
                        calculatedFloorNumber = numberFromName;
                    }
                    else if (isLastOrSecondLastLevel && isHeightValid)
                    {
                        calculatedFloorNumber = 100;

                        if (calculatedFloorNumber != 100)
                        {
                            calculatedFloorNumber += 1;
                        }
                    }
                    else if (calculatedFloorNumber <= 0 && elevation <= -LEVEL_MIN_HEIGHT)
                    {
                        calculatedFloorNumber = BASEMENT_NUMBER;
                    }
                    else if (calculatedFloorNumber <= 0 && elevation <= LEVEL_MIN_HEIGHT)
                    {
                        calculatedFloorNumber = GROUND_NUMBER;
                    }
                    else if (calculatedFloorNumber > 0 && isHeightValid)
                    {
                        calculatedFloorNumber += 1;
                    }
                    else if (calculatedFloorNumber >= 99)
                    {
                        calculatedFloorNumber += 1;
                    }
                }

                levelDictionary[calculatedFloorNumber] = level;

                previousElevation = elevation;
            }

            return levelDictionary;
        }



        /// <summary>
        /// Преобразует высоту уровня в метры
        /// </summary>
        private static double GetElevationInMeters(Level level)
        {
            double elevationInFeet = level.get_Parameter(BuiltInParameter.LEVEL_ELEV).AsDouble();
            return Math.Round(UnitManager.FootToMm(elevationInFeet) / 1000.0, 3);
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
        private static bool IsLastOrSecondLastLevel(int currentIndex, int totalCount)
        {
            return currentIndex > 5 && currentIndex > totalCount - 3; // Последние два уровня
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