namespace LevelAssignment
{
    public sealed record FloorModel
    {
        public readonly List<Level> ContainedLevels;
        public double InternalElevation { get; private set; }
        public double ProjectElevation { get; private set; }
        public string DisplayName { get; private set; }
        public double Height { get; internal set; }
        public int Index { get; private set; }


        public FloorModel(int floorNumber, IEnumerable<Level> floorLevels)
        {
            ContainedLevels = [.. floorLevels.OrderBy(l => l.Elevation)];
            Level baseLevel = ContainedLevels.FirstOrDefault();
            ProjectElevation = baseLevel.ProjectElevation;
            InternalElevation = baseLevel.Elevation;
            DisplayName = baseLevel.Name;
            Index = floorNumber;
        }
    }



}
