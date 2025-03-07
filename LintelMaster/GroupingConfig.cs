namespace LintelMaster
{
    /// <summary>
    /// Конфигурация для алгоритма группировки и маркировки элементов
    /// </summary>
    public class GroupingConfig
    {
        #region Параметры группировки

        /// <summary>
        /// Допуск для толщины стены (мм)
        /// </summary>
        public int ThickTolerance { get; set; } = 25;

        /// <summary>
        /// Допуск для ширины проема (мм)
        /// </summary>
        public int WidthTolerance { get; set; } = 50;

        /// <summary>
        /// Допуск для высоты (мм)
        /// </summary>
        public int HeightTolerance { get; set; } = 300;

        /// <summary>
        /// Максимальное допустимое отклонение для объединения групп (мм)
        /// </summary>
        public int MaxTotalDeviation { get; set; } = 500;

        /// <summary>
        /// Вес для толщины стены (значение от 0 до 1)
        /// </summary>
        public double ThickWeight { get; set; } = 0.6;

        /// <summary>
        /// Вес для ширины проема (значение от 0 до 1)
        /// </summary>
        public double WidthWeight { get; set; } = 0.3;

        /// <summary>
        /// Вес для высоты (значение от 0 до 1)
        /// </summary>
        public double HeightWeight { get; set; } = 0.1;

        /// <summary>
        /// Вес для фактора размера группы (значение от 0 до 1)
        /// </summary>
        public double GroupSizeWeight { get; set; } = 0.4;

        /// <summary>
        /// Минимальный размер группы для рассмотрения к объединению
        /// </summary>
        public int MinGroupThreshold { get; set; } = 5;

        /// <summary>
        /// Оптимальный целевой размер группы
        /// </summary>
        public int OptimalGroupSize { get; set; } = 10;

        #endregion

        #region Параметры маркировки

        /// <summary>
        /// Префикс для марок
        /// </summary>
        public string Prefix { get; set; } = "ПР-";

        /// <summary>
        /// Имя параметра для толщины стены
        /// </summary>
        public string ThickParameter { get; set; } = "Толщина стены";

        /// <summary>
        /// Имя параметра для ширины проема
        /// </summary>
        public string WidthParameter { get; set; } = "Ширина проема";

        /// <summary>
        /// Имя параметра для высоты
        /// </summary>
        public string HeightParameter { get; set; } = "Высота";

        /// <summary>
        /// Имя параметра для марки
        /// </summary>
        public string MarkParam { get; set; } = "BI_марка_изделия";

        /// <summary>
        /// Наименование семейства перемычек
        /// </summary>
        public string FamilyName { get; set; } = "Перемычка";

        #endregion
    }
}