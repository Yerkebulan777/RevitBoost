using LintelMaster;
using RevitUtils;

/// <summary>
/// Оптимизированный класс для унификации перемычек
/// </summary>
public class OptimizedLintelUnifier(MarkConfig config)
{
    private const int MinGroupThreshold = 5;
    private const int OptimalGroupSize = 10;



    private readonly int _thickTolerance = config.ThickTolerance;
    private readonly int _widthTolerance = config.WidthTolerance;
    private readonly int _heightTolerance = config.HeightTolerance;
    private readonly int _totalDeviation = config.MaxTotalDeviation;

    /// <summary>
    /// Структура для хранения результатов анализа групп
    /// </summary>
    private struct GroupAnalysisResult
    {
        public List<SizeKey> GroupsToUnify { get; set; }
        public Dictionary<SizeKey, int> GroupSizes { get; set; }
    }

    /// <summary>
    /// Выполняет унификацию групп перемычек в один проход
    /// </summary>
    /// <param name="lintels">Список экземпляров перемычек</param>
    /// <param name="threshold">Пороговое значение для унификации</param>
    /// <returns>Словарь унифицированных групп</returns>
    public Dictionary<SizeKey, List<LintelData>> UnifyGroups(Dictionary<SizeKey, List<LintelData>> groupedLintels)
    {
        // Анализируем группы и определяем, какие нужно унифицировать
        if (AnalyzeGroups(groupedLintels, out GroupAnalysisResult result))
        {
            UnionSize unionFind = ConsolidateSmallGroups(result);

            return CreateUnifiedGroups(groupedLintels, unionFind);
        }

        return groupedLintels;
    }



    /// <summary>
    /// Определяет группы для унификации на основе пороговых  значений
    /// </summary>
    private bool AnalyzeGroups(Dictionary<SizeKey, List<LintelData>> groups, out GroupAnalysisResult result)
    {
        List<SizeKey> groupsToUnify = [];

        Dictionary<SizeKey, int> groupSizes = [];

        if (groups.Count >= MinGroupThreshold)
        {
            foreach (KeyValuePair<SizeKey, List<LintelData>> pair in groups)
            {
                int size = pair.Value.Count;
                groupSizes[pair.Key] = size;

                if (size < OptimalGroupSize)
                {
                    groupsToUnify.Add(pair.Key);
                }
            }
        }

        result = new GroupAnalysisResult
        {
            GroupsToUnify = groupsToUnify,
            GroupSizes = groupSizes
        };

        return groupsToUnify.Count > 0;
    }

    /// <summary>
    /// Унифицирует малые группы
    /// </summary>
    private UnionSize ConsolidateSmallGroups(GroupAnalysisResult result)
    {
        Dictionary<SizeKey, int> groupSizes = result.GroupSizes;
        List<SizeKey> allGroups = groupSizes.Keys.ToList();
        UnionSize unionFind = new(allGroups);
        HashSet<SizeKey> processedGroups = [];

        // Приоритизируем обработку наименьших групп сначала
        foreach (SizeKey sourceKey in result.GroupsToUnify.OrderBy(g => groupSizes[g]))
        {
            // Пропускаем обработанные группы
            if (!processedGroups.Contains(sourceKey))
            {
                SizeKey? bestTarget = FindBestMatchingGroup(sourceKey, allGroups, unionFind);

                if (bestTarget != null && processedGroups.Add(sourceKey))
                {
                    unionFind.Union(sourceKey, bestTarget.Value, groupSizes);
                }
            }
        }

        return unionFind;
    }


    /// <summary>
    /// Находит наилучшую группу для объединения
    /// </summary>
    private SizeKey? FindBestMatchingGroup(SizeKey sourceKey, List<SizeKey> allGroups, UnionSize unionFind)
    {
        double bestScore = double.MaxValue;
        SizeKey? bestTarget = null;

        foreach (SizeKey targetKey in allGroups)
        {
            // Пропускаем сравнение с собой и уже объединенными группами
            if (sourceKey.Equals(targetKey) || sourceKey.Equals(unionFind.FindRoot(targetKey)))
            {
                continue;
            }

            // Проверяем допуски и вычисляем оценку схожести
            if (IsSizeWithinTolerances(sourceKey, targetKey))
            {
                double score = CalculateSimilarityScore(sourceKey, targetKey);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = targetKey;
                }
            }
        }

        return bestTarget;
    }

    /// <summary>
    /// Проверяет, находится ли разница между размерами в пределах допустимых отклонений
    /// </summary>
    public bool IsSizeWithinTolerances(SizeKey source, SizeKey target)
    {
        // Быстрая проверка для отсечения явно различающихся размеров
        if (Math.Abs(source.Thick - target.Thick) > _thickTolerance ||
            Math.Abs(source.Width - target.Width) > _widthTolerance ||
            Math.Abs(source.Height - target.Height) > _heightTolerance)
        {
            return false;
        }

        // Проверка общего допуска
        double totalDifference =
            Math.Abs(source.Thick - target.Thick) +
            Math.Abs(source.Width - target.Width) +
            Math.Abs(source.Height - target.Height);

        return totalDifference < _totalDeviation;
    }

    /// <summary>
    /// Вычисляет взвешенную оценку схожести между двумя наборами размеров
    /// </summary>
    private double CalculateSimilarityScore(SizeKey source, SizeKey target)
    {
        const double weightMultiplier = 10;

        double thickScore = Math.Abs(source.Thick - target.Thick) * Math.Pow(weightMultiplier, 2);
        double widthScore = Math.Abs(source.Width - target.Width) * weightMultiplier;
        double heightScore = Math.Abs(source.Height - target.Height);

        return thickScore + widthScore + heightScore;
    }

    /// <summary>
    /// Создает новый словарь с унифицированными группами
    /// </summary>
    private Dictionary<SizeKey, List<LintelData>> CreateUnifiedGroups(Dictionary<SizeKey, List<LintelData>> originalGroups, UnionSize unionFind)
    {
        Dictionary<SizeKey, List<LintelData>> unifiedGroups = [];
        Dictionary<SizeKey, SizeKey> keyToRoot = [];

        // Заранее находим корневые группы для каждого ключа
        foreach (SizeKey key in originalGroups.Keys)
        {
            keyToRoot[key] = unionFind.FindRoot(key);
        }

        // Создаем новые группы на основе корневых ключей
        foreach (KeyValuePair<SizeKey, List<LintelData>> entry in originalGroups)
        {
            SizeKey originalKey = entry.Key;
            SizeKey rootKey = keyToRoot[originalKey];

            // Инициализируем группу, если она еще не существует
            if (!unifiedGroups.TryGetValue(rootKey, out List<LintelData> group))
            {
                group = [];
                unifiedGroups[rootKey] = group;
            }

            // Добавляем данные в группу, обновляя SizeKey
            foreach (LintelData lintelData in entry.Value)
            {
                lintelData.GroupName = rootKey;
                group.Add(lintelData);
            }
        }

        return unifiedGroups;
    }



}


