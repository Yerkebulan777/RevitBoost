using RevitUtils;
using System.Text.RegularExpressions;


namespace LevelAssignment
{
    public sealed class LevelNumberCalculator
    {
        private const int FIRST_NUMBER = 1; // Номер первого этажа
        private const int BASEMENT_NUMBER = -1; // Номер подземного этажа
        private const double SLAB_THICKNESS = 0.25; // Номинальная толщина плиты (м)
        private const double LEVEL_MIN_HEIGHT = 2.0; // Минимальная высота этажа (м)

        private readonly int[] specialFloorNumbers = [99, 100, 101]; // Специальные номера этажей

        private static readonly Regex levelNumberRegex = new(@"\d+", RegexOptions.Compiled);

        public Dictionary<int, Level> CalculateLevelNumberData(List<Level> levels)
        {
            double previousElevation = 0;
            int calculatedFloorNumber = 0;

            Dictionary<int, Level> levelDictionary = [];

            foreach (Level currentLevel in levels.OrderBy(x => x.Elevation))
            {
                double elevation = GetElevationInMeters(currentLevel);

                int numberFromName = ExtractNumberFromName(currentLevel.Name);
                bool validName = IsValidFloorNumber(numberFromName, levels.Count);
                bool validHeight = Math.Abs(elevation - previousElevation) > LEVEL_MIN_HEIGHT;

                if (validName && validHeight && calculatedFloorNumber < numberFromName)
                {
                    calculatedFloorNumber = numberFromName;
                }
                else if (elevation > SLAB_THICKNESS)
                {
                    calculatedFloorNumber = BASEMENT_NUMBER;
                }
                else if (elevation < LEVEL_MIN_HEIGHT)
                {
                    calculatedFloorNumber = FIRST_NUMBER;
                }
                else if (calculatedFloorNumber > 0 && validHeight)
                {
                    calculatedFloorNumber += 1;
                }

                levelDictionary[calculatedFloorNumber] = currentLevel;

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
        /// Проверяет валидность номера этажа из имени уровня
        /// </summary>
        private bool IsValidFloorNumber(int numberFromName, int totalLevels)
        {
            return numberFromName != 0 && (numberFromName < totalLevels || specialFloorNumbers.Contains(numberFromName));
        }



    }
}

