using LintelMaster;
using RevitUtils;

/// <summary>
/// Унификатор перемычек с использованием алгоритма на основе графов
/// </summary>
public class GraphBasedLintelUnifier
{
    private readonly MarkConfig _config;
    private readonly int _optimalGroupSize;

    public GraphBasedLintelUnifier(MarkConfig config, int optimalGroupSize = 5)
    {
        _config = config;
        _optimalGroupSize = optimalGroupSize;
    }

    /// <summary>
    /// Выполняет унификацию групп перемычек с помощью графового алгоритма
    /// </summary>
    public Dictionary<SizeKey, List<LintelData>> UnifyGroups(List<FamilyInstance> lintels, int threshold)
    {
        // Категоризация перемычек
        var initialGroups = CategorizeLintelData(lintels);

        if (initialGroups.Count <= 1)
            return initialGroups;

        // Анализ групп и разделение на малые и большие
        var (smallGroups, largeGroups, groupSizes) = AnalyzeGroups(initialGroups, threshold);

        if (smallGroups.Count == 0)
            return initialGroups;

        // Построение графа совместимости
        var compatibilityGraph = BuildCompatibilityGraph(smallGroups, largeGroups);

        // Применение алгоритма максимального паросочетания
        var unionFind = ApplyGraphMatching(compatibilityGraph, smallGroups, largeGroups, groupSizes);

        // Создание унифицированных групп
        return CreateUnifiedGroups(initialGroups, unionFind);
    }

    /// <summary>
    /// Анализирует группы и разделяет их на малые и большие
    /// </summary>
    private (List<SizeKey> SmallGroups, List<SizeKey> LargeGroups, Dictionary<SizeKey, int> GroupSizes)
        AnalyzeGroups(Dictionary<SizeKey, List<LintelData>> groups, int threshold)
    {
        var smallGroups = new List<SizeKey>();
        var largeGroups = new List<SizeKey>();
        var groupSizes = new Dictionary<SizeKey, int>();

        foreach (var pair in groups)
        {
            var key = pair.Key;
            var size = pair.Value.Count;

            groupSizes[key] = size;

            if (size < threshold)
                smallGroups.Add(key);
            else
                largeGroups.Add(key);
        }

        return (smallGroups, largeGroups, groupSizes);
    }

    // Дополнительные методы для реализации графового алгоритма
    // ...
}
