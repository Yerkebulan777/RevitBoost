namespace LintelMaster;

/// <summary>
/// Класс для хранения информации о совпадении групп
/// </summary>
public readonly record struct GroupMatch
{
    /// <summary>
    /// Исходная группа (малая)
    /// </summary>
    public SizeKey Source { get; }

    /// <summary>
    /// Целевая группа для объединения
    /// </summary>
    public SizeKey Target { get; }

    /// <summary>
    /// Оценка сходства (меньше — лучше)
    /// </summary>
    public double Score { get; }

    public GroupMatch(SizeKey source, SizeKey target, double score)
    {
        Source = source;
        Target = target;
        Score = score;
    }
}
