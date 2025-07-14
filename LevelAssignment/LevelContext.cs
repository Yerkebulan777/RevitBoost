namespace LevelAssignment
{
    internal readonly record struct LevelContext
    {
        public required int Index { get; init; }
        public required string LevelName { get; init; }
        public required int CurrentNumber { get; init; }
        public required int TotalLevelCount { get; init; }
        public required double ElevationDelta { get; init; }
        public required double DisplayElevation { get; init; }
        public required double PreviousElevation { get; init; }
    }
}
