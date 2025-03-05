using LintelMaster;
using RevitUtils;

/// <summary>
/// Оптимизированный класс для унификации перемычек
/// </summary>
public class OptimizedLintelUnifier
{
    private readonly MarkConfig _config;

    // Кэшированные параметры из конфигурации для быстрого доступа
    private readonly string _thickParam;
    private readonly string _widthParam;
    private readonly string _heightParam;
    private readonly int _deviation;
    private readonly int _thickTolerance;
    private readonly int _widthTolerance;
    private readonly int _heightTolerance;
    private readonly int _roundBase;
    private readonly int _minCount;

    public OptimizedLintelUnifier(MarkConfig config)
    {
        _config = config;

        _thickParam = config.ThickParameter;
        _widthParam = config.WidthParameter;
        _heightParam = config.HeightParameter;
        _deviation = config.MaxTotalDeviation;
        _thickTolerance = config.ThickTolerance;
        _widthTolerance = config.WidthTolerance;
        _heightTolerance = config.HeightTolerance;
        _roundBase = config.RoundBase;
        _minCount = config.MinCount;
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

        // Если групп меньше двух, унификация не требуется
        if (groupedLintels.Count <= 1)
        {
            return groupedLintels;
        }

        // Получаем размеры групп и малые группы в один проход
        Dictionary<SizeKey, int> groupSizes = new();
        List<SizeKey> smallGroups = new();

        foreach (KeyValuePair<SizeKey, List<LintelData>> group in groupedLintels)
        {
            int size = group.Value.Count;
            groupSizes[group.Key] = size;

            if (size < threshold)
            {
                smallGroups.Add(group.Key);
            }
        }

        // Если нет малых групп, унификация не требуется
        if (smallGroups.Count == 0)
        {
            return groupedLintels;
        }

        // Находим оптимальные пары для объединения
        List<SizeKey> allGroups = groupSizes.Keys.ToList();
        UnionSize unionFind = new(allGroups);

        // Оптимизированный поиск пар для унификации
        OptimizedFindAndApplyMatches(smallGroups, allGroups, unionFind, groupSizes);

        // Строим результирующий словарь унифицированных групп
        return BuildUnifiedGroups(groupedLintels, unionFind);
    }

    /// <summary>
    /// Находит и применяет оптимальные пары для унификации в один проход
    /// </summary>
    private void OptimizedFindAndApplyMatches(List<SizeKey> smallGroups, List<SizeKey> allGroups, UnionSize unionFind, Dictionary<SizeKey, int> groupSizes)
    {
        HashSet<SizeKey> processedGroups = new();

        // Приоритизируем обработку наименьших групп сначала
        foreach (SizeKey sourceKey in smallGroups.OrderBy(g => groupSizes[g]))
        {
            if (!processedGroups.Contains(sourceKey))
            {
                // Находим корневую группу для текущей группы
                SizeKey sourceRoot = unionFind.FindRoot(sourceKey);

                // Если группа уже достигла минимального размера, пропускаем
                int currentSize = CalculateCurrentGroupSize(sourceRoot, groupSizes, unionFind);
                if (currentSize >= _minCount)
                {
                    continue;
                }

                // Ищем лучшее соответствие для текущей группы
                SizeKey? bestTarget = null;
                double bestScore = double.MaxValue;

                foreach (SizeKey targetKey in allGroups)
                {
                    // Пропускаем сравнение с самой собой или уже объединенными группами
                    if (sourceKey.Equals(targetKey) ||
                        unionFind.FindRoot(sourceKey).Equals(unionFind.FindRoot(targetKey)))
                    {
                        continue;
                    }

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

                // Если нашли подходящую пару - объединяем
                if (bestTarget.HasValue)
                {
                    unionFind.Union(sourceKey, bestTarget.Value, groupSizes);
                    _ = processedGroups.Add(sourceKey);
                }
            }
        }
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
            double thickRound = RoundSize(LintelUtils.GetParamValue(lintel, _thickParam));
            double widthRound = RoundSize(LintelUtils.GetParamValue(lintel, _widthParam));
            double heightRound = RoundSize(LintelUtils.GetParamValue(lintel, _heightParam));

            SizeKey dimensions = new(thickRound, widthRound, heightRound);

            // Создаем объект данных перемычки
            LintelData lintelData = new(lintel)
            {
                Thick = thickRound,
                Width = widthRound,
                Height = heightRound,
                Size = dimensions
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
    /// Округляет размер до базового значения
    /// </summary>
    private double RoundSize(double sizeInFeet)
    {
        return UnitManager.FootToRoundedMm(sizeInFeet, _roundBase);
    }

    /// <summary>
    /// Рассчитывает текущий размер группы с учетом всех выполненных объединений
    /// </summary>
    private int CalculateCurrentGroupSize(SizeKey rootKey, Dictionary<SizeKey, int> groupSizes, UnionSize unionFind)
    {
        return groupSizes
            .Where(entry => unionFind.FindRoot(entry.Key).Equals(rootKey))
            .Sum(entry => entry.Value);
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

        return totalDifference < _deviation;
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
    private Dictionary<SizeKey, List<LintelData>> BuildUnifiedGroups(Dictionary<SizeKey, List<LintelData>> originalGroups, UnionSize unionFind)
    {
        Dictionary<SizeKey, List<LintelData>> unifiedGroups = new();

        // Оптимизация: предварительно находим все корневые группы
        Dictionary<SizeKey, List<SizeKey>> rootToOriginal = new();

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

            List<LintelData> unifiedGroup = new();
            unifiedGroups[rootKey] = unifiedGroup;

            // Обновляем и добавляем элементы из всех оригинальных групп
            foreach (SizeKey originalKey in originalKeys)
            {
                List<LintelData> originalLintelData = originalGroups[originalKey];

                foreach (LintelData lintel in originalLintelData)
                {
                    lintel.Size = rootKey;
                    unifiedGroup.Add(lintel);
                }
            }
        }

        return unifiedGroups;
    }


}