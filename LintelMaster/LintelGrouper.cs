namespace LintelMaster
{
    /// <summary>
    /// Адаптер для группировки перемычек
    /// </summary>
    public class LintelGrouper
    {
        private readonly GroupMerger _merger;

        /// <summary>
        /// Создает новый группировщик перемычек
        /// </summary>
        public LintelGrouper(GroupingConfig config)
        {
            // Создаем универсальный группировщик с существующей конфигурацией
            _merger = new GroupMerger(config);
        }

        /// <summary>
        /// Выполняет унификацию групп перемычек
        /// </summary>
        public Dictionary<SizeKey, List<LintelData>> UnifyGroups(Dictionary<SizeKey, List<LintelData>> groups)
        {
            // Делегируем работу универсальному группировщику
            return _merger.Merge(groups);
        }
    }
}