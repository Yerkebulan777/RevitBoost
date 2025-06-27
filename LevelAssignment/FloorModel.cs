namespace LevelAssignment
{
    public sealed record FloorModel
    {
        public readonly List<Level> ContainedLevels;
        public Outline BoundaryOutline { get; set; }
        public double InternalElevation { get; }
        public double ProjectElevation { get; }
        public string DisplayName { get; }
        public int FloorNumber { get; }


        public FloorModel(int floorNumber, IEnumerable<Level> floorLevels)
        {
            ContainedLevels = [.. floorLevels.OrderBy(l => l.Elevation)];
            Level baseLevel = ContainedLevels.FirstOrDefault();
            ProjectElevation = baseLevel.ProjectElevation;
            InternalElevation = baseLevel.Elevation;
            DisplayName = baseLevel.Name;
            FloorNumber = floorNumber;
        }
    }



}
