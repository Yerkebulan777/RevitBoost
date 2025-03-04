namespace LintelMaster;

/// <summary>
/// Управляет группировкой перемычек на основе заданных допусков
/// </summary>
public class LintelGrouper
{
    private readonly MarkConfig _config;

    public LintelGrouper(MarkConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Группирует перемычки в соответствии с параметрами допусков, определенными в MarkConfig
    /// </summary>
    public List<List<LintelData>> GroupLintels(List<LintelData> lintels)
    {
        // Сортируем перемычки по наиболее значимым параметрам для эффективной группировки
        var sortedLintels = lintels.OrderBy(l => l.Thick)
                                  .ThenBy(l => l.Width)
                                  .ThenBy(l => l.Height)
                                  .ToList();

        var groups = new List<List<LintelData>>();

        foreach (var lintel in sortedLintels)
        {
            bool addedToExistingGroup = false;

            // Пытаемся добавить в существующую группу, соответствующую требованиям допусков
            foreach (var group in groups)
            {
                if (CanAddToGroup(group, lintel))
                {
                    group.Add(lintel);
                    addedToExistingGroup = true;
                    break;
                }
            }

            // Создаем новую группу, если перемычка не подходит для существующих групп
            if (!addedToExistingGroup)
            {
                groups.Add(new List<LintelData> { lintel });
            }
        }

        // Назначаем идентификаторы групп (опционально)
        AssignGroupIdentifiers(groups);

        return groups;
    }

    /// <summary>
    /// Определяет, можно ли добавить перемычку в существующую группу на основе ограничений допусков
    /// </summary>
    private bool CanAddToGroup(List<LintelData> group, LintelData candidate)
    {
        // Используем первый элемент группы в качестве эталона
        var reference = group[0];

        // Проверяем допуски для каждого параметра
        bool thicknessWithinTolerance = Math.Abs(candidate.Thick - reference.Thick) <= _config.ThickTolerance;
        bool widthWithinTolerance = Math.Abs(candidate.Width - reference.Width) <= _config.WidthTolerance;
        bool heightWithinTolerance = Math.Abs(candidate.Height - reference.Height) <= _config.HeightTolerance;

        // Вычисляем общее отклонение относительно эталонных значений
        double totalDeviation = Math.Abs(candidate.Thick - reference.Thick) +
                               Math.Abs(candidate.Width - reference.Width) +
                               Math.Abs(candidate.Height - reference.Height);

        // Убеждаемся, что общее отклонение находится в допустимых пределах
        bool totalDeviationWithinLimit = totalDeviation <= _config.MaxTotalDeviation;

        return thicknessWithinTolerance && widthWithinTolerance && heightWithinTolerance && totalDeviationWithinLimit;
    }

    /// <summary>
    /// Назначает идентификаторы групп сгруппированным перемычкам
    /// </summary>
    private void AssignGroupIdentifiers(List<List<LintelData>> groups)
    {
        // Реализация зависит от структуры SizeKey
        // Например:
        for (int i = 0; i < groups.Count; i++)
        {
            var representativeLintel = groups[i][0];
            var sizeKey = new SizeKey(); // Предполагается, что у SizeKey есть конструктор по умолчанию

            // Устанавливаем соответствующие значения для группы
            // Этот блок требует корректировки в соответствии с реализацией SizeKey

            // Назначаем одинаковый SizeKey всем перемычкам в группе
            foreach (var lintel in groups[i])
            {
                lintel.Size = sizeKey;
            }
        }
    }
}