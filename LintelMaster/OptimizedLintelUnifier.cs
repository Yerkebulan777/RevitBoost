namespace LintelMaster;

/// <summary>
/// Оптимизированный класс для унификации перемычек с учетом размеров групп и приоритетов параметров
/// </summary>
public class OptimizedLintelUnifier
{
    // Минимальное значение для проверки необходимости унификации группы
    private const int MinGroupThreshold = 5;

    // Целевой размер группы после унификации
    private const int OptimalGroupSize = 10;

    // Конфигурационные параметры допусков из MarkingConfig
    private readonly int _thickTolerance;
    private readonly int _widthTolerance;
    private readonly int _heightTolerance;
    private readonly int _maxTotalDeviation;

    // Веса параметров для оценки схожести (нормализованные, сумма = 1.0)
    private readonly double _thickWeight;
    private readonly double _widthWeight;
    private readonly double _heightWeight;

    // Вес для фактора размера группы (0.0 - не влияет, 1.0 - максимальное влияние)
    private readonly double _groupSizeWeight;

    /// <summary>
    /// Ограничивает значение указанным диапазоном
    /// </summary>
    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    /// <summary>
    /// Структура для хранения результатов анализа групп
    /// </summary>
    private class GroupAnalysisResult
    {
        // Группы, которые следует рассмотреть для унификации
        public List<SizeKey> GroupsToUnify { get; set; }

        // Размеры всех групп (кол-во элементов в каждой группе)
        public Dictionary<SizeKey, int> GroupSizes { get; set; }

        // Общее количество элементов во всех группах
        public int TotalElements { get; set; }

        // Все группы (ключи), включая большие
        public List<SizeKey> AllGroups { get; set; }
    }

    /// <summary>
    /// Создает новый экземпляр унификатора перемычек с настраиваемыми весами параметров
    /// </summary>
    public OptimizedLintelUnifier(
        MarkingConfig config,
        double thickWeight = 0.6,
        double widthWeight = 0.3,
        double heightWeight = 0.1,
        double groupSizeWeight = 0.4)
    {
        _thickTolerance = config.ThickTolerance;
        _widthTolerance = config.WidthTolerance;
        _heightTolerance = config.HeightTolerance;
        _maxTotalDeviation = config.MaxTotalDeviation;

        // Нормализуем веса, чтобы их сумма равнялась 1.0
        double totalWeight = thickWeight + widthWeight + heightWeight;
        _thickWeight = thickWeight / totalWeight;
        _widthWeight = widthWeight / totalWeight;
        _heightWeight = heightWeight / totalWeight;

        // Ограничиваем вес размера группы от 0 до 1
        _groupSizeWeight = Clamp(groupSizeWeight, 0, 1);
    }

    /// <summary>
    /// Выполняет унификацию групп перемычек на основе их размеров и весов параметров
    /// </summary>
    /// <param name="groupedLintels">Словарь сгруппированных по размерам перемычек</param>
    /// <returns>Словарь унифицированных групп</returns>
    public Dictionary<SizeKey, List<LintelData>> UnifyGroups(Dictionary<SizeKey, List<LintelData>> groupedLintels)
    {
        // Если групп меньше двух, унификация не требуется
        if (groupedLintels.Count <= 1)
            return groupedLintels;

        // Анализируем группы и определяем, какие нужно унифицировать
        GroupAnalysisResult analysisResult = AnalyzeGroups(groupedLintels);

        // Если нет групп для унификации, возвращаем исходное группирование
        if (analysisResult.GroupsToUnify.Count == 0)
            return groupedLintels;

        // Выполняем унификацию групп
        UnionSize unionResult = MergeGroups(analysisResult);

        // Создаем новый словарь с унифицированными группами
        return CreateUnifiedGroups(groupedLintels, unionResult);
    }

    /// <summary>
    /// Анализирует группы и определяет, какие нужно унифицировать
    /// </summary>
    private GroupAnalysisResult AnalyzeGroups(Dictionary<SizeKey, List<LintelData>> groups)
    {
        List<SizeKey> groupsToUnify = new List<SizeKey>();
        Dictionary<SizeKey, int> groupSizes = new Dictionary<SizeKey, int>();
        List<SizeKey> allGroups = new List<SizeKey>();
        int totalElements = 0;

        // Для каждой группы определяем ее размер
        foreach (var pair in groups)
        {
            SizeKey key = pair.Key;
            int size = pair.Value.Count;

            // Добавляем ключ в список всех групп
            allGroups.Add(key);

            // Сохраняем размер группы
            groupSizes[key] = size;

            // Суммируем для общего количества элементов
            totalElements += size;

            // Если размер группы меньше оптимального, добавляем в список для унификации
            if (size < OptimalGroupSize)
            {
                groupsToUnify.Add(key);
            }
        }

        return new GroupAnalysisResult
        {
            GroupsToUnify = groupsToUnify,
            GroupSizes = groupSizes,
            TotalElements = totalElements,
            AllGroups = allGroups
        };
    }

    /// <summary>
    /// Выполняет объединение малых групп в более крупные на основе их схожести
    /// </summary>
    private UnionSize MergeGroups(GroupAnalysisResult analysisResult)
    {
        // Создаем структуру для отслеживания объединений
        UnionSize unionFind = new UnionSize(analysisResult.AllGroups);

        // Множество групп, которые еще не были обработаны
        HashSet<SizeKey> pendingGroups = new HashSet<SizeKey>(analysisResult.GroupsToUnify);

        // Флаг, указывающий, были ли выполнены объединения на текущей итерации
        bool mergesPerformed = true;

        // Продолжаем пока есть необработанные группы и происходят объединения
        while (mergesPerformed && pendingGroups.Count > 0)
        {
            mergesPerformed = false;

            // Сортируем группы от наименьшей к наибольшей для приоритетной обработки
            List<SizeKey> sortedGroups = pendingGroups
                .OrderBy(g => GetEffectiveGroupSize(g, unionFind, analysisResult.GroupSizes))
                .ToList();

            // Создаем список групп, которые будут удалены из ожидающих после этой итерации
            List<SizeKey> groupsToRemove = new List<SizeKey>();

            // Обрабатываем каждую группу по порядку
            foreach (SizeKey sourceKey in sortedGroups)
            {
                // Пропускаем группы, которые уже не являются корнями (объединены с другими)
                if (!unionFind.IsRoot(sourceKey))
                {
                    groupsToRemove.Add(sourceKey);
                    continue;
                }

                // Ищем наилучшую группу для объединения
                SizeKey? bestTargetKey = FindBestMatchingGroup(
                    sourceKey,
                    unionFind,
                    analysisResult);

                // Если нашли подходящую группу
                if (bestTargetKey != null && !sourceKey.Equals(bestTargetKey.Value))
                {
                    // Выполняем объединение групп
                    unionFind.Union(sourceKey, bestTargetKey.Value, analysisResult.GroupSizes);

                    // Отмечаем обработанную группу для удаления
                    groupsToRemove.Add(sourceKey);

                    // Отмечаем, что были выполнены объединения на этой итерации
                    mergesPerformed = true;
                }
                else
                {
                    // Если не нашли подходящую группу, удаляем из ожидающих
                    groupsToRemove.Add(sourceKey);
                }
            }

            // Удаляем обработанные группы из множества ожидающих
            foreach (SizeKey key in groupsToRemove)
            {
                pendingGroups.Remove(key);
            }

            // Обновляем эффективные размеры групп для следующей итерации
            UpdateEffectiveGroupSizes(unionFind, analysisResult.GroupSizes);
        }

        return unionFind;
    }

    /// <summary>
    /// Находит наилучшую группу для объединения с указанной группой
    /// </summary>
    private SizeKey? FindBestMatchingGroup(
        SizeKey sourceKey,
        UnionSize unionFind,
        GroupAnalysisResult analysisResult)
    {
        double bestScore = double.MaxValue;
        SizeKey? bestTarget = null;

        int sourceGroupSize = GetEffectiveGroupSize(sourceKey, unionFind, analysisResult.GroupSizes);

        foreach (SizeKey targetKey in analysisResult.AllGroups)
        {
            // Пропускаем сравнение с собой и с группами, с которыми уже объединены
            if (sourceKey.Equals(targetKey) || unionFind.FindRoot(targetKey).Equals(unionFind.FindRoot(sourceKey)))
            {
                continue;
            }

            // Проверяем допуски и вычисляем оценку схожести
            if (IsSizeWithinTolerances(sourceKey, targetKey))
            {
                int targetGroupSize = GetEffectiveGroupSize(targetKey, unionFind, analysisResult.GroupSizes);

                double score = CalculateSimilarityScore(
                    sourceKey,
                    targetKey,
                    sourceGroupSize,
                    targetGroupSize,
                    analysisResult.TotalElements);

                // Запоминаем группу с наилучшей (наименьшей) оценкой
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
    private bool IsSizeWithinTolerances(SizeKey source, SizeKey target)
    {
        // Вычисляем абсолютные отклонения по каждому параметру
        double thickDifference = Math.Abs(source.Thick - target.Thick);
        double widthDifference = Math.Abs(source.Width - target.Width);
        double heightDifference = Math.Abs(source.Height - target.Height);

        // Проверяем, что все отклонения находятся в пределах допусков
        bool individualTolerances =
            thickDifference <= _thickTolerance &&
            widthDifference <= _widthTolerance &&
            heightDifference <= _heightTolerance;

        // Также проверяем общее отклонение
        double totalDifference = thickDifference + widthDifference + heightDifference;

        return individualTolerances && totalDifference < _maxTotalDeviation;
    }

    /// <summary>
    /// Вычисляет взвешенную оценку схожести между двумя группами с учетом всех факторов
    /// </summary>
    private double CalculateSimilarityScore(
        SizeKey source,
        SizeKey target,
        int sourceGroupSize,
        int targetGroupSize,
        int totalElements)
    {
        // 1. Нормализуем различия в размерах относительно допусков
        double thickDiff = Math.Abs(source.Thick - target.Thick) / _thickTolerance;
        double widthDiff = Math.Abs(source.Width - target.Width) / _widthTolerance;
        double heightDiff = Math.Abs(source.Height - target.Height) / _heightTolerance;

        // 2. Вычисляем взвешенную оценку по размерам (меньше - лучше)
        double dimensionScore =
            (thickDiff * _thickWeight) +
            (widthDiff * _widthWeight) +
            (heightDiff * _heightWeight);

        // 3. Рассчитываем соотношение размера группы к общему количеству элементов
        double sourceRatio = (double)sourceGroupSize / totalElements;

        // 4. Фактор размера группы (для малых групп ближе к 1, для больших ближе к 0)
        // Чем меньше группа, тем больше значение (1 - sourceRatio)
        double groupSizeFactor = (1 - sourceRatio) * _groupSizeWeight;

        // 5. Финальная оценка: уменьшаем "штраф" за различия для малых групп
        // Для малых групп, оценка будет меньше, что повышает шанс их объединения
        return dimensionScore * (1 - groupSizeFactor);
    }

    /// <summary>
    /// Возвращает эффективный размер группы с учетом всех объединенных групп
    /// </summary>
    private int GetEffectiveGroupSize(SizeKey key, UnionSize unionFind, Dictionary<SizeKey, int> groupSizes)
    {
        // Находим корневой элемент для группы
        SizeKey rootKey = unionFind.FindRoot(key);

        // Суммируем размеры всех групп с тем же корнем
        int totalSize = 0;
        foreach (var entry in groupSizes)
        {
            if (unionFind.FindRoot(entry.Key).Equals(rootKey))
            {
                totalSize += entry.Value;
            }
        }

        return totalSize;
    }

    /// <summary>
    /// Обновляет информацию о размерах групп после объединения
    /// </summary>
    private void UpdateEffectiveGroupSizes(UnionSize unionFind, Dictionary<SizeKey, int> groupSizes)
    {
        // Кэшируем эффективные размеры групп для более быстрого доступа
        Dictionary<SizeKey, int> effectiveSizes = new Dictionary<SizeKey, int>();

        foreach (var key in groupSizes.Keys.ToList())
        {
            SizeKey rootKey = unionFind.FindRoot(key);

            // Вычисляем размер только если еще не вычислили
            if (!effectiveSizes.ContainsKey(rootKey))
            {
                effectiveSizes[rootKey] = GetEffectiveGroupSize(rootKey, unionFind, groupSizes);
            }
        }
    }

    /// <summary>
    /// Создает новый словарь с унифицированными группами на основе результатов объединения
    /// </summary>
    private Dictionary<SizeKey, List<LintelData>> CreateUnifiedGroups(
        Dictionary<SizeKey, List<LintelData>> originalGroups,
        UnionSize unionFind)
    {
        Dictionary<SizeKey, List<LintelData>> unifiedGroups = new Dictionary<SizeKey, List<LintelData>>();
        Dictionary<SizeKey, SizeKey> keyToRoot = new Dictionary<SizeKey, SizeKey>();

        // Определяем корневую группу для каждого ключа
        foreach (SizeKey key in originalGroups.Keys)
        {
            keyToRoot[key] = unionFind.FindRoot(key);
        }

        // Создаем новые группы на основе корневых ключей
        foreach (var entry in originalGroups)
        {
            SizeKey originalKey = entry.Key;
            SizeKey rootKey = keyToRoot[originalKey];

            // Инициализируем группу, если она еще не существует
            if (!unifiedGroups.TryGetValue(rootKey, out List<LintelData> group))
            {
                group = new List<LintelData>();
                unifiedGroups[rootKey] = group;
            }

            // Добавляем данные в группу, обновляя информацию о группе
            foreach (LintelData lintelData in entry.Value)
            {
                lintelData.GroupName = rootKey;
                group.Add(lintelData);
            }
        }

        return unifiedGroups;
    }
}