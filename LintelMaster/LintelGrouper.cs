namespace LintelMaster;

/// <summary>
/// Управляет группировкой перемычек на основе заданных допусков
/// </summary>
public class LintelGrouper
{
    private readonly MarkConfig _config;
    private int deviation => _config.MaxTotalDeviation;
    private int thickTolerance => _config.ThickTolerance;
    private int widthTolerance => _config.WidthTolerance;
    private int heightTolerance => _config.HeightTolerance;

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
        List<LintelData> sortedLintels = lintels.OrderBy(l => l.Thick).ThenBy(l => l.Width).ThenBy(l => l.Height).ToList();

        List<List<LintelData>> groups = [];

        foreach (LintelData lintel in sortedLintels)
        {
            bool addedToExistingGroup = false;

            // Пытаемся добавить в существующую группу, соответствующую требованиям допусков
            foreach (List<LintelData> group in groups)
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
                groups.Add([lintel]);
            }
        }

        return groups;
    }

    /// <summary>
    /// Определяет, можно ли добавить перемычку в существующую группу на основе ограничений допусков
    /// </summary>
    private bool CanAddToGroup(List<LintelData> group, LintelData candidate)
    {
        // Используем первый элемент группы в качестве эталона
        LintelData reference = group[0];

        // Проверяем допуски для каждого параметра
        bool thicknessWithinTolerance = Math.Abs(candidate.Thick - reference.Thick) <= thickTolerance;
        bool widthWithinTolerance = Math.Abs(candidate.Width - reference.Width) <= widthTolerance;
        bool heightWithinTolerance = Math.Abs(candidate.Height - reference.Height) <= heightTolerance;

        // Вычисляем общее отклонение относительно эталонных значений
        double totalDeviation = Math.Abs(candidate.Thick - reference.Thick) +
                               Math.Abs(candidate.Width - reference.Width) +
                               Math.Abs(candidate.Height - reference.Height);

        // Убеждаемся, что общее отклонение находится в допустимых пределах
        bool totalDeviationWithinLimit = totalDeviation <= deviation;

        return thicknessWithinTolerance && widthWithinTolerance && heightWithinTolerance && totalDeviationWithinLimit;
    }

    /// <summary>
    /// Проверяет, находятся ли размеры в пределах допусков
    /// </summary>
    protected bool IsSizeTolerances(SizeKey source, SizeKey target)
    {
        double diffThick = Math.Abs(source.Thick - target.Thick);
        double diffWidth = Math.Abs(source.Width - target.Width);
        double diffHeight = Math.Abs(source.Height - target.Height);

        // Проверяем индивидуальные допуски
        bool individualTolerances =
            diffThick < thickTolerance &&
            diffWidth < widthTolerance &&
            diffHeight < heightTolerance;

        // Проверяем общий допуск
        double totalDeviation = diffThick + diffWidth + diffHeight;

        return individualTolerances && totalDeviation < deviation;
    }



}