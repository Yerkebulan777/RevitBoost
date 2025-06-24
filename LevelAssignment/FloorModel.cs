namespace LevelAssignment
{
    public sealed class FloorModel
    {
        public double ProjectElevation { get; }
        public string DisplayName { get; }
        public int FloorNumber { get; }

        public readonly List<Level> FloorLevels;

        public FloorModel(int floorNumber, IEnumerable<Level> floorLevels)
        {
            FloorLevels = [.. floorLevels.OrderBy(l => l.Elevation)];
            Level baseLevel = FloorLevels.FirstOrDefault();
            ProjectElevation = baseLevel.ProjectElevation;
            DisplayName = baseLevel.Name;
            FloorNumber = floorNumber;
        }

        /// <summary>
        /// Проверяет, содержит ли этаж несколько уровней
        /// </summary>
        public bool HasMultipleLevels => FloorLevels.Count > 1;


    }
}
