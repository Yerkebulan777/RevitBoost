namespace LevelAssignment
{
    internal readonly record struct LevelContext
    {
        public required int Index { get; init; }
        public required int TotalLevels { get; init; }
        public required string LevelName { get; init; }
        public required double DisplayElevation { get; init; }
        public required double PreviousElevation { get; init; }
        public required double ElevationDifference { get; init; }
    }
}
