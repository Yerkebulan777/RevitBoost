using LintelMaster;
using RevitUtils;

/// <summary>
/// Класс, отвечающий за алгоритм унификации перемычек
/// </summary>
public class LintelUnifier(MarkConfig config)
{
    const int minGroupCount = 5;
    const int optimalGroupSize = 15;
    private string thickParam => config.ThickParameter;
    private string widthParam => config.WidthParameter;
    private string heightParam => config.HeightParameter;
    private int thickTolerance => config.ThickTolerance;
    private int widthTolerance => config.WidthTolerance;
    private int heightTolerance => config.HeightTolerance;
    private int totalDeviation => config.MaxTotalDeviation;

    /// <summary>
    /// Выполняет унификацию групп перемычек
    /// </summary>
    /// <param name="groups">Исходный словарь групп</param>
    /// <param name="threshold">Пороговое значение для унификации</param>
    /// <returns>Словарь унифицированных групп</returns>
    public Dictionary<SizeKey, List<LintelData>> UnifyGroups(List<FamilyInstance> lintels, int threshold)
    {
        Dictionary<SizeKey, List<LintelData>> groupedLintels = CategorizeLintelData(lintels);

        if (groupedLintels.Count > minGroupCount)
        {
            // Получаем размеры групп и малые группы

            Dictionary<SizeKey, int> groupSizes = [];

            List<SizeKey> groupsToUnify = [];

            foreach (KeyValuePair<SizeKey, List<LintelData>> group in groupedLintels)
            {
                int size = group.Value.Count;
                groupSizes[group.Key] = size;

                if (size < threshold)
                {
                    groupsToUnify.Add(group.Key);
                }
            }

            if (groupsToUnify.Count > 0)
            {
                // Находим оптимальные пары для унификации
                List<GroupMatch> matchesToApply = DetectGroupsToUnify(groupsToUnify, allGroupKeys, threshold);

                // Применяем найденные пары к структуре объединения
                var unionFind = ApplyGroupMatches(matchesToApply, unionFind, groupSizes);

                // Создаем новый словарь с унифицированными группами
                return BuildUnifiedGroups(groupedLintels, unionFind);
            }
        }

        return groupedLintels;

    }

    /// <summary>
    /// Категоризирует перемычки по их размерам
    /// </summary>
    /// <param name="lintels">Список экземпляров перемычек</param>
    /// <returns>Словарь групп перемычек, где ключ - размеры, значение - список перемычек</returns>
    public Dictionary<SizeKey, List<LintelData>> CategorizeLintelData(List<FamilyInstance> lintels)
    {
        Dictionary<SizeKey, List<LintelData>> result = [];

        foreach (FamilyInstance lintel in lintels)
        {
            // Получаем и округляем размеры
            double thickRound = UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, thickParam));
            double widthRound = UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, widthParam), 50);
            double heightRound = UnitManager.FootToRoundedMm(LintelUtils.GetParamValue(lintel, heightParam), 100);

            SizeKey dimensions = new(thickRound, widthRound, heightRound);

            // Создаем объект данных перемычки
            LintelData lintelData = new(lintel)
            {
                Thick = thickRound,
                Width = widthRound,
                Height = heightRound,
                GroupKey = dimensions
            };

            // Добавляем в соответствующую группу
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
    /// Находит оптимальные пары групп для унификации
    /// </summary>
    /// <param name="groupsToUnify">Группы, требующие унификации</param>
    /// <param name="allGroupKeys">Все доступные группы</param>
    /// <param name="threshold">Пороговое значение размера группы</param>
    /// <returns>Список пар групп для унификации</returns>
    private List<GroupMatch> DetectGroupsToUnify(List<SizeKey> groupsToUnify, List<SizeKey> allGroupKeys, int threshold)
    {
        // Находим оптимальные пары для объединения
        List<SizeKey> allGroups = groupSizes.Keys.ToList();
        UnionSize unionFind = new(allGroups);


        // Получаем все потенциальные пары для унификации
        List<GroupMatch> potentialMatches = FindPotentialMatches(groupsToUnify, allGroupKeys);

        if (potentialMatches.Count == 0)
        {
            return [];
        }

        // Сортируем по оценке схожести (от лучшего к худшему)
        return potentialMatches.OrderBy(m => m.Score).ToList();
    }

    /// <summary>
    /// Применяет найденные пары к структуре объединения групп
    /// </summary>
    /// <param name="matches">Список пар для объединения</param>
    /// <param name="unionFind">Структура отслеживания объединений</param>
    /// <param name="groupSizes">Размеры групп</param>
    /// <returns>Обновленная структура отслеживания объединений</returns>
    private UnionSize ApplyGroupMatches(List<GroupMatch> matches, UnionSize unionFind, Dictionary<SizeKey, int> groupSizes)
    {
        HashSet<SizeKey> processedGroups = [];

        foreach (GroupMatch match in matches)
        {
            SizeKey sourceKey = match.Source;
            SizeKey targetKey = match.Target;

            // Пропускаем уже унифицированные группы
            if (processedGroups.Contains(sourceKey))
            {
                continue;
            }

            // Получаем текущие корневые группы
            SizeKey sourceRoot = unionFind.FindRoot(sourceKey);
            SizeKey targetRoot = unionFind.FindRoot(targetKey);

            // Объединяем только если группы еще не были объединены
            if (!sourceRoot.Equals(targetRoot))
            {
                // Проверяем, не превысит ли объединенная группа порог минимального размера
                int currentSize = CalculateCurrentGroupSize(sourceRoot, groupSizes, unionFind);

                if (currentSize < 5)
                {
                    unionFind.Union(sourceKey, targetKey, groupSizes);
                    _ = processedGroups.Add(sourceKey);
                }
            }
        }

        return unionFind;
    }

    /// <summary>
    /// Создает новый словарь с унифицированными группами
    /// </summary>
    /// <param name="originalGroups">Исходный словарь групп</param>
    /// <param name="unionFind">Структура отслеживания объединений</param>
    /// <returns>Новый словарь с унифицированными группами</returns>
    private Dictionary<SizeKey, List<LintelData>> BuildUnifiedGroups(Dictionary<SizeKey, List<LintelData>> originalGroups, UnionSize unionFind)
    {
        Dictionary<SizeKey, List<LintelData>> unifiedGroups = [];

        // Для каждой исходной группы
        foreach (KeyValuePair<SizeKey, List<LintelData>> entry in originalGroups)
        {
            SizeKey originalKey = entry.Key;
            SizeKey rootKey = unionFind.FindRoot(originalKey);

            // Создаем новую группу, если еще не существует
            if (!unifiedGroups.ContainsKey(rootKey))
            {
                unifiedGroups[rootKey] = [];
            }

            // Обновляем размеры в данных и добавляем в унифицированную группу
            foreach (LintelData lintel in entry.Value)
            {
                // Обновляем размеры в соответствии с корневой группой
                lintel.GroupKey = rootKey;
                unifiedGroups[rootKey].Add(lintel);
            }
        }

        return unifiedGroups;
    }

    /// <summary>
    /// Находит потенциальные пары групп для унификации
    /// </summary>
    /// <returns>Список потенциальных пар с оценкой схожести</returns>
    private List<GroupMatch> FindPotentialMatches(List<SizeKey> groupsToUnify, List<SizeKey> allGroups)
    {
        List<GroupMatch> matches = [];

        foreach (SizeKey sourceKey in groupsToUnify)
        {
            double bestScore = double.MaxValue;

            SizeKey? bestTarget = null;

            // Для каждой группы ищем наилучшее соответствие
            foreach (SizeKey targetKey in allGroups)
            {
                // Пропускаем сравнение с самой собой
                if (sourceKey.Equals(targetKey))
                {
                    continue;
                }

                // Проверяем, подходят ли размеры по заданным допускам
                if (IsSizeWithinTolerances(sourceKey, targetKey))
                {
                    // Рассчитываем оценку схожести
                    double score = CalculateSimilarityScore(sourceKey, targetKey);

                    // Запоминаем лучшее соответствие
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestTarget = targetKey;
                    }
                }
            }

            // Если нашли подходящую пару - добавляем в список
            if (bestTarget.HasValue)
            {
                matches.Add(new GroupMatch(sourceKey, bestTarget.Value, bestScore));
            }
        }

        return matches;
    }

    /// <summary>
    /// Рассчитывает текущий размер группы с учетом всех выполненных объединений
    /// </summary>
    private int CalculateCurrentGroupSize(SizeKey rootKey, Dictionary<SizeKey, int> groupSizes, UnionSize unionFind)
    {
        int totalSize = 0;

        foreach (KeyValuePair<SizeKey, int> entry in groupSizes)
        {
            if (unionFind.FindRoot(entry.Key).Equals(rootKey))
            {
                totalSize += entry.Value;
            }
        }

        return totalSize;
    }

    /// <summary>
    /// Проверяет, находится ли разница между размерами в пределах допустимых отклонений
    /// </summary>
    public bool IsSizeWithinTolerances(SizeKey source, SizeKey target)
    {
        // Вычисляем отклонения по каждому параметру
        double thickDifference = Math.Abs(source.Thick - target.Thick);
        double widthDifference = Math.Abs(source.Width - target.Width);
        double heightDifference = Math.Abs(source.Height - target.Height);

        // Проверка индивидуальных допусков
        bool withinIndividualTolerances =
            thickDifference <= thickTolerance &&
            widthDifference <= widthTolerance &&
            heightDifference <= heightTolerance;

        double totalDifference = thickDifference + widthDifference + heightDifference;

        return withinIndividualTolerances && totalDifference < totalDeviation;
    }

    /// <summary>
    /// Вычисляет взвешенную оценку схожести между двумя наборами размеров
    /// </summary>
    private double CalculateSimilarityScore(SizeKey source, SizeKey target)
    {
        double totalScore = 0;
        double weightMultiplier = 10; // Множитель для весов параметров

        // Применяем веса в соответствии с приоритетом параметров
        for (int idx = 0; idx < 3; idx++)
        {
            // Больший вес для более приоритетных параметров
            double weight = Math.Pow(weightMultiplier, 3 - idx - 1);

            switch (idx)
            {
                case (int)GroupingParameter.Thick:
                    totalScore += Math.Abs(source.Thick - target.Thick) * weight;
                    break;
                case (int)GroupingParameter.Width:
                    totalScore += Math.Abs(source.Width - target.Width) * weight;
                    break;
                case (int)GroupingParameter.Height:
                    totalScore += Math.Abs(source.Height - target.Height) * weight;
                    break;
            }
        }

        return totalScore;
    }



}