namespace LintelMaster;

/// <summary>
/// Конфигурация алгоритма маркировки
/// </summary>
public class MarkConfig
{
    /// <summary>
    /// Минимальное количество элементов для "большой" группы
    /// </summary>
    public int MinCount { get; set; } = 5;

    /// <summary>
    /// Базовое значение для округления (мм)
    /// </summary>
    public int RoundBase { get; set; } = 50;

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

}


/// <summary>
/// Параметры перемычки для группировки
/// </summary>
public enum GroupingParameter
{
    /// <summary>
    /// Толщина стены
    /// </summary>
    Thick,

    /// <summary>
    /// Ширина проема
    /// </summary>
    Width,

    /// <summary>
    /// Высота проема
    /// </summary>
    Height
}