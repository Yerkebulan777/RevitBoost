namespace LintelMaster
{

    /// <summary>
    /// Универсальный класс для объединения групп по схожим размерам
    /// </summary>
    public class GroupMerger
    {
        // Границы для объединения групп
        private readonly int _minGroupThreshold;
        private readonly int _optimalGroupSize;

        // Допуски размеров
        private readonly int _thickTolerance;
        private readonly int _widthTolerance;
        private readonly int _heightTolerance;
        private readonly int _maxTotalDeviation;

        // Веса параметров (нормализованные, сумма = 1.0)
        private readonly double _thickWeight;
        private readonly double _widthWeight;
        private readonly double _heightWeight;

        // Вес фактора размера группы (0.0 - 1.0)
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
        private class GroupAnalysis
        {
            // Группы для объединения (меньше порогового размера)
            public List<SizeKey> SmallGroups { get; set; }

            // Размеры всех групп
            public Dictionary<SizeKey, int> Sizes { get; set; }

            // Общее количество элементов
            public int TotalElements { get; set; }

            // Все ключи групп
            public List<SizeKey> AllKeys { get; set; }
        }

        /// <summary>
        /// Создает новый экземпляр группировщика по размерам
        /// </summary>
        public GroupMerger(GroupingConfig config)
        {
            _thickTolerance = config.ThickTolerance;
            _widthTolerance = config.WidthTolerance;
            _heightTolerance = config.HeightTolerance;
            _maxTotalDeviation = config.MaxTotalDeviation;

            _minGroupThreshold = config.MinGroupThreshold;
            _optimalGroupSize = config.OptimalGroupSize;

            // Нормализуем веса размеров
            double totalWeight = config.ThickWeight + config.WidthWeight + config.HeightWeight;
            _thickWeight = config.ThickWeight / totalWeight;
            _widthWeight = config.WidthWeight / totalWeight;
            _heightWeight = config.HeightWeight / totalWeight;

            // Ограничиваем вес размера группы
            _groupSizeWeight = Clamp(config.GroupSizeWeight, 0, 1);
        }

        /// <summary>
        /// Выполняет объединение групп на основе схожести размеров
        /// </summary>
        public Dictionary<SizeKey, List<LintelData>> Merge(Dictionary<SizeKey, List<LintelData>> groups)
        {
            // Если групп меньше двух, объединение не требуется
            if (groups.Count <= 1)
                return groups;

            // Анализируем группы и определяем кандидатов для объединения
            GroupAnalysis analysis = AnalyzeGroups(groups);

            // Если нет групп для объединения, возвращаем исходное группирование
            if (analysis.SmallGroups.Count == 0)
                return groups;

            // Выполняем объединение групп
            UnionSize unions = MergeSmallGroups(analysis);

            // Создаем новый словарь с объединенными группами
            return CreateMergedGroups(groups, unions);
        }

        /// <summary>
        /// Анализирует группы и определяет кандидатов для объединения
        /// </summary>
        private GroupAnalysis AnalyzeGroups(Dictionary<SizeKey, List<LintelData>> groups)
        {
            List<SizeKey> smallGroups = new List<SizeKey>();
            Dictionary<SizeKey, int> sizes = new Dictionary<SizeKey, int>();
            List<SizeKey> allKeys = new List<SizeKey>();
            int totalElements = 0;

            // Анализируем каждую группу
            foreach (var pair in groups)
            {
                SizeKey key = pair.Key;
                int size = pair.Value.Count;

                allKeys.Add(key);
                sizes[key] = size;
                totalElements += size;

                // Если размер группы меньше оптимального, добавляем в кандидаты
                if (size < _optimalGroupSize)
                {
                    smallGroups.Add(key);
                }
            }

            return new GroupAnalysis
            {
                SmallGroups = smallGroups,
                Sizes = sizes,
                TotalElements = totalElements,
                AllKeys = allKeys
            };
        }

        /// <summary>
        /// Выполняет объединение малых групп в более крупные
        /// </summary>
        private UnionSize MergeSmallGroups(GroupAnalysis analysis)
        {
            // Структура для отслеживания объединений
            UnionSize unionFind = new UnionSize(analysis.AllKeys);

            // Группы, ожидающие обработки
            HashSet<SizeKey> pendingGroups = new HashSet<SizeKey>(analysis.SmallGroups);

            // Флаг наличия объединений на текущей итерации
            bool mergesPerformed = true;

            // Продолжаем, пока есть группы для обработки и происходят объединения
            while (mergesPerformed && pendingGroups.Count > 0)
            {
                mergesPerformed = false;

                // Сортируем от наименьшей к наибольшей
                List<SizeKey> sortedGroups = pendingGroups
                    .OrderBy(g => GetEffectiveSize(g, unionFind, analysis.Sizes))
                    .ToList();

                // Группы для удаления из ожидающих после этой итерации
                List<SizeKey> groupsToRemove = new List<SizeKey>();

                // Обрабатываем каждую группу
                foreach (SizeKey sourceKey in sortedGroups)
                {
                    // Пропускаем уже объединенные группы
                    if (!unionFind.IsRoot(sourceKey))
                    {
                        groupsToRemove.Add(sourceKey);
                        continue;
                    }

                    // Ищем наилучшую группу для объединения
                    SizeKey? bestMatch = FindBestMatch(
                        sourceKey,
                        unionFind,
                        analysis);

                    // Если нашли подходящую группу
                    if (bestMatch != null && !sourceKey.Equals(bestMatch.Value))
                    {
                        // Объединяем группы
                        unionFind.Union(sourceKey, bestMatch.Value, analysis.Sizes);
                        groupsToRemove.Add(sourceKey);
                        mergesPerformed = true;
                    }
                    else
                    {
                        // Если не нашли - удаляем из ожидающих
                        groupsToRemove.Add(sourceKey);
                    }
                }

                // Удаляем обработанные группы
                foreach (SizeKey key in groupsToRemove)
                {
                    pendingGroups.Remove(key);
                }

                // Обновляем размеры групп после объединения
                UpdateGroupSizes(unionFind, analysis.Sizes);
            }

            return unionFind;
        }

        /// <summary>
        /// Находит наилучшую группу для объединения
        /// </summary>
        private SizeKey? FindBestMatch(
            SizeKey sourceKey,
            UnionSize unionFind,
            GroupAnalysis analysis)
        {
            double bestScore = double.MaxValue;
            SizeKey? bestMatch = null;

            int sourceSize = GetEffectiveSize(sourceKey, unionFind, analysis.Sizes);

            foreach (SizeKey targetKey in analysis.AllKeys)
            {
                // Пропускаем сравнение с собой и уже объединенными группами
                if (sourceKey.Equals(targetKey) ||
                    unionFind.FindRoot(targetKey).Equals(unionFind.FindRoot(sourceKey)))
                {
                    continue;
                }

                // Проверяем допуски размеров
                if (IsWithinTolerance(sourceKey, targetKey))
                {
                    int targetSize = GetEffectiveSize(targetKey, unionFind, analysis.Sizes);

                    // Вычисляем оценку схожести
                    double score = CalculateScore(
                        sourceKey,
                        targetKey,
                        sourceSize,
                        targetSize,
                        analysis.TotalElements);

                    // Запоминаем лучшую группу
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestMatch = targetKey;
                    }
                }
            }

            return bestMatch;
        }

        /// <summary>
        /// Проверяет, находятся ли размеры в пределах допусков
        /// </summary>
        private bool IsWithinTolerance(SizeKey source, SizeKey target)
        {
            // Отклонения по каждому параметру
            double thickDiff = Math.Abs(source.Thick - target.Thick);
            double widthDiff = Math.Abs(source.Width - target.Width);
            double heightDiff = Math.Abs(source.Height - target.Height);

            // Проверка индивидуальных допусков
            bool withinLimits =
                thickDiff <= _thickTolerance &&
                widthDiff <= _widthTolerance &&
                heightDiff <= _heightTolerance;

            // Проверка общего отклонения
            double totalDiff = thickDiff + widthDiff + heightDiff;

            return withinLimits && totalDiff < _maxTotalDeviation;
        }

        /// <summary>
        /// Вычисляет оценку схожести между двумя группами
        /// </summary>
        private double CalculateScore(
            SizeKey source,
            SizeKey target,
            int sourceSize,
            int targetSize,
            int totalElements)
        {
            // Нормализуем различия относительно допусков
            double thickDiff = Math.Abs(source.Thick - target.Thick) / _thickTolerance;
            double widthDiff = Math.Abs(source.Width - target.Width) / _widthTolerance;
            double heightDiff = Math.Abs(source.Height - target.Height) / _heightTolerance;

            // Взвешенная оценка размеров
            double sizeScore =
                thickDiff * _thickWeight +
                widthDiff * _widthWeight +
                heightDiff * _heightWeight;

            // Фактор размера группы: чем меньше группа, тем выше приоритет объединения
            double sizeRatio = (double)sourceSize / totalElements;
            double sizeFactor = (1 - sizeRatio) * _groupSizeWeight;

            // Финальная оценка (меньше = лучше)
            return sizeScore * (1 - sizeFactor);
        }

        /// <summary>
        /// Возвращает эффективный размер группы с учетом объединений
        /// </summary>
        private int GetEffectiveSize(SizeKey key, UnionSize unionFind, Dictionary<SizeKey, int> sizes)
        {
            // Находим корень группы
            SizeKey rootKey = unionFind.FindRoot(key);

            // Суммируем размеры всех групп с тем же корнем
            int totalSize = 0;
            foreach (var entry in sizes)
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
        private void UpdateGroupSizes(UnionSize unionFind, Dictionary<SizeKey, int> sizes)
        {
            // Кэш размеров для быстрого доступа
            Dictionary<SizeKey, int> effectiveSizes = new Dictionary<SizeKey, int>();

            foreach (var key in sizes.Keys.ToList())
            {
                SizeKey rootKey = unionFind.FindRoot(key);

                if (!effectiveSizes.ContainsKey(rootKey))
                {
                    effectiveSizes[rootKey] = GetEffectiveSize(rootKey, unionFind, sizes);
                }
            }
        }

        /// <summary>
        /// Создает новый словарь с объединенными группами
        /// </summary>
        private Dictionary<SizeKey, List<LintelData>> CreateMergedGroups(
            Dictionary<SizeKey, List<LintelData>> originalGroups,
            UnionSize unionFind)
        {
            Dictionary<SizeKey, List<LintelData>> mergedGroups = new Dictionary<SizeKey, List<LintelData>>();
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

                // Инициализируем группу
                if (!mergedGroups.TryGetValue(rootKey, out List<LintelData> group))
                {
                    group = new List<LintelData>();
                    mergedGroups[rootKey] = group;
                }

                // Добавляем данные в группу
                foreach (LintelData itemData in entry.Value)
                {
                    itemData.GroupName = rootKey;
                    group.Add(itemData);
                }
            }

            return mergedGroups;
        }
    }
}