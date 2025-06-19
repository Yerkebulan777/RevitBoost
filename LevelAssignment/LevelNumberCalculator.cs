using RevitUtils;
using System.Diagnostics;
using System.Text.RegularExpressions;


namespace LevelAssignment
{
    public class LevelNumberCalculator
    {
        private const int GROUND_FLOOR = 1; // Номер первого этажа
        private const int FIRST_BASEMENT = -1; // Номер подземного этажа
        private const double SLAB_THICKNESS = 0.25; // Номинальная толщина плиты (м)
        private const double LEVEL_MIN_HEIGHT = 2.0; // Минимальная высота этажа (м)
        private readonly Regex levelNumberRegex = new(@"\d+", RegexOptions.Compiled);
        private readonly int[] specialFloorNumbers = { 99, 100, 101 };

        /// <summary>
        /// Определяет номер этажа для заданного уровня
        /// </summary>

        /// <returns>Номер этажа (положительный - надземные, отрицательный - подземные)</returns>
        public int GetLevelNumber(Level level, List<Level> levels)
        {
            int currentLevelId = level.Id.IntegerValue;

            List<Level> sortedLevels = [.. levels.OrderBy(x => x.Elevation)];

            int currentFloorNumber = 0;
            double previousElevation = 0;

            foreach (Level currentLevel in sortedLevels)
            {
                double elevation = GetElevationInMeters(currentLevel);

                if (Math.Floor(elevation) >= 0)
                {
                    int numberFromName = ExtractNumberFromName(currentLevel.Name);
                    bool validName = IsValidFloorNumber(numberFromName, levels.Count);
                    bool validHeight = Math.Abs(elevation - previousElevation) > LEVEL_MIN_HEIGHT;

                    // Обновляем референсную высоту
                    if (validName || validHeight)
                    {
                        previousElevation = elevation;
                    }

                    // Определяем номер этажа
                    currentFloorNumber = CalculateFloorNumber(elevation, numberFromName, currentFloorNumber, validHeight, validName);

                    // Если это наш целевой уровень - возвращаем результат
                    if (currentLevel.Id.IntegerValue == currentLevelId)
                    {
                        LogResult(currentLevel.Name, numberFromName, elevation, currentFloorNumber);
                        return currentFloorNumber;
                    }
                }
            }

            return -1;
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

        /// <summary>
        /// Вычисляет номер этажа на основе высоты и других параметров
        /// </summary>
        private static int CalculateFloorNumber(double elevation, int? numberFromName, int currentNumber, bool validHeight, bool validName)
        {
            // Уровень около нулевой отметки
            if (Math.Abs(elevation) < SLAB_THICKNESS)
            {
                return GROUND_FLOOR;
            }

            // Подземные уровни
            if (elevation < -SLAB_THICKNESS)
            {
                return currentNumber >= 0 ? FIRST_BASEMENT : validHeight ? currentNumber - 1 : currentNumber;
            }

            // Надземные уровни
            return elevation > SLAB_THICKNESS
                ? validName && numberFromName.HasValue ? numberFromName.Value : validHeight ? currentNumber + 1 : currentNumber
                : currentNumber;
        }

        /// <summary>
        /// Логирование результата для отладки
        /// </summary>
        private static void LogResult(string levelName, int? numberFromName, double elevation, int result)
        {
            Debug.WriteLine($"Уровень: {levelName}");
            Debug.WriteLine($"Число из имени: {numberFromName?.ToString() ?? "Нет"}");
            Debug.WriteLine($"Высота: {elevation:F3} м");
            Debug.WriteLine($"Результат: {result}");
            Debug.WriteLine("----------------------------------------");
        }
    }
}

