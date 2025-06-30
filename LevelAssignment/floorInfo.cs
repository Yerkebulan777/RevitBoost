namespace LevelAssignment
{
    public sealed record FloorInfo
    {

        public readonly List<Level> ContainedLevels;
        public BoundingBoxXYZ BoundingBox { get; internal set; }
        public double InternalElevation { get; private set; }
        public double ProjectElevation { get; private set; }
        public string DisplayName { get; private set; }
        public double Height { get; internal set; }
        public int Index { get; private set; }


        public FloorInfo(int floorNumber, IEnumerable<Level> floorLevels)
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
