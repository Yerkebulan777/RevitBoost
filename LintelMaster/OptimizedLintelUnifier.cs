using LintelMaster;
using RevitUtils;
using System.Collections.Generic;

/// <summary>
/// Оптимизированный класс для унификации перемычек
/// </summary>
public class OptimizedLintelUnifier(MarkConfig config)
{
    private readonly string _thickParam = config.ThickParameter;
    private readonly string _widthParam = config.WidthParameter;
    private readonly string _heightParam = config.HeightParameter;
    private readonly int _totalDeviation = config.MaxTotalDeviation;
    private readonly int _thickTolerance = config.ThickTolerance;
    private readonly int _widthTolerance = config.WidthTolerance;
    private readonly int _heightTolerance = config.HeightTolerance;

    private const int MinGroupThreshold = 5;
    private const int OptimalGroupSize = 10;

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
    public Dictionary<SizeKey, List<LintelData>> UnifyGroups(List<FamilyInstance> lintels, int threshold)
    {
        // Группируем перемычки по размерам
        Dictionary<SizeKey, List<LintelData>> groupedLintels = CategorizeLintelData(lintels);

        // Анализируем группы и определяем, какие нужно унифицировать
        if (AnalyzeGroups(groupedLintels, out GroupAnalysisResult result))
        {

            // Pезультирующий словарь унифицированных групп
            return UnifiedGroups(groupedLintels, unionFind);
        }

        return groupedLintels;
    }


    /// <summary>
    /// Категоризирует перемычки по их размерам
    /// </summary>
    public Dictionary<SizeKey, List<LintelData>> CategorizeLintelData(List<FamilyInstance> lintels)
    {
        // Предварительно рассчитываем ожидаемую ёмкость словаря
        Dictionary<SizeKey, List<LintelData>> result = new(Math.Min(lintels.Count, 50));

        foreach (FamilyInstance lintel in lintels)
        {
            // Получаем и округляем размеры (один вызов функции вместо трёх)
            double thickRound = UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _thickParam));
            double widthRound = UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _widthParam));
            double heightRound = UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, _heightParam));

            SizeKey dimensions = new(thickRound, widthRound, heightRound);

            // Создаем объект данных перемычки
            LintelData lintelData = new(lintel)
            {
                Thick = thickRound,
                Width = widthRound,
                Height = heightRound,
                DimensionsGroup = dimensions
            };

            // Более эффективно используем TryGetValue
            if (!result.TryGetValue(dimensions, out List<LintelData> group))
            {
                group = [];
                result[dimensions] = group;
            }

            group.Add(lintelData);
        }

        return result;
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
    /// Находит и применяет оптимальные пары для унификации в один проход
    /// </summary>
    private void OptimizedFindAndApplyMatches(List<SizeKey> smallGroups, Dictionary<SizeKey, int> groupSizes)
    {
        HashSet<SizeKey> processedGroups = [];

        List<SizeKey> allGroups = groupSizes.Keys.ToList();

        UnionSize unionFind = new(groupSizes.Keys.ToList());

        // Приоритизируем обработку наименьших групп сначала
        foreach (SizeKey sourceKey in smallGroups.OrderBy(g => groupSizes[g]))
        {
            if (!processedGroups.Contains(sourceKey))
            {
                // Находим корневую группу для текущей группы
                SizeKey sourceRoot = unionFind.FindRoot(sourceKey);

                // Если группа уже достигла отимльного размера, пропускаем
                int currentSize = CalculateCurrentGroupSize(sourceRoot, groupSizes, unionFind);

                if (currentSize > OptimalGroupSize)
                {
                    continue;
                }

                SizeKey? bestTarget = null;
                // Ищем соответствие для группы
                double bestScore = double.MaxValue;

                foreach (SizeKey targetKey in allGroups)
                {
                    if (!sourceKey.Equals(targetKey))
                    {
                        // Пропускаем сравнение с уже объединенными группами
                        if (!sourceKey.Equals(unionFind.FindRoot(targetKey)))
                        {
                            // Проверяем, подходят ли размеры по допускам
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
                    }
                }

                // Если нашли подходящую пару - объединяем
                if (bestTarget.HasValue)
                {
                    _ = processedGroups.Add(sourceKey);
                    _ = unionFind.Union(sourceKey, bestTarget.Value, groupSizes);
                }
            }
        }
    }


    /// <summary>
    /// Рассчитывает текущий размер группы с учетом всех выполненных объединений
    /// </summary>
    private int CalculateCurrentGroupSize(SizeKey rootKey, Dictionary<SizeKey, int> groupSizes, UnionSize unionFind)
    {
        return groupSizes.Where(entry => unionFind.FindRoot(entry.Key).Equals(rootKey)).Sum(entry => entry.Value);
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

        // Проверка общего допуска, только если индивидуальные допуски прошли
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
        // Оптимизированный расчет оценки схожести
        const double weightMultiplier = 10;

        double thickScore = Math.Abs(source.Thick - target.Thick) * Math.Pow(weightMultiplier, 2);
        double widthScore = Math.Abs(source.Width - target.Width) * weightMultiplier;
        double heightScore = Math.Abs(source.Height - target.Height);

        return thickScore + widthScore + heightScore;
    }

    /// <summary>
    /// Создает новый словарь с унифицированными группами
    /// </summary>
    private Dictionary<SizeKey, List<LintelData>> UnifiedGroups(Dictionary<SizeKey, List<LintelData>> originalGroups, UnionSize unionFind)
    {
        Dictionary<SizeKey, List<LintelData>> unifiedGroups = [];

        // Оптимизация: предварительно находим все корневые группы
        Dictionary<SizeKey, List<SizeKey>> rootToOriginal = [];

        foreach (SizeKey key in originalGroups.Keys)
        {
            SizeKey rootKey = unionFind.FindRoot(key);

            if (!rootToOriginal.TryGetValue(rootKey, out List<SizeKey> originals))
            {
                originals = [];
                rootToOriginal[rootKey] = originals;
            }

            originals.Add(key);
        }

        // Итерируем по корневым группам для более эффективного построения результата
        foreach (KeyValuePair<SizeKey, List<SizeKey>> entry in rootToOriginal)
        {
            SizeKey rootKey = entry.Key;
            List<SizeKey> originalKeys = entry.Value;

            List<LintelData> unifiedGroup = [];
            unifiedGroups[rootKey] = unifiedGroup;

            // Обновляем и добавляем элементы из всех оригинальных групп
            foreach (SizeKey originalKey in originalKeys)
            {
                List<LintelData> originalLintelData = originalGroups[originalKey];

                foreach (LintelData lintel in originalLintelData)
                {
                    lintel.DimensionsGroup = rootKey;
                    unifiedGroup.Add(lintel);
                }
            }
        }

        return unifiedGroups;
    }


}