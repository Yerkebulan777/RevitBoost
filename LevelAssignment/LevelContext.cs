namespace LevelAssignment
{
    internal record struct LevelContext
    {
        public required int Index { get; init; }
        public required int FloorNumber { get; set; }
        public required string LevelName { get; init; }
        public required int TotalLevelCount { get; init; }
        public required double ElevationDelta { get; init; }
        public required double DisplayElevation { get; init; }
        public required double PreviousElevation { get; init; }
    }
}
