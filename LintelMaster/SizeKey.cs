namespace LintelMaster;

/// <summary>
/// Структура для хранения и сравнения ключевых размеров перемычки
/// </summary>
public readonly struct SizeKey : IEquatable<SizeKey>
{
    /// <summary>
    /// Толщина стены
    /// </summary>
    public int ThickInMm { get; }

    /// <summary>
    /// Ширина проема
    /// </summary>
    public int WidthInMm { get; }

    /// <summary>
    /// Высота
    /// </summary>
    public int HeightInMm { get; }

    /// <summary>
    /// Создает новый экземпляр ключа размеров
    /// </summary>
    /// <param name="thick">Толщина стены</param>
    /// <param name="width">Ширина проема</param>
    /// <param name="height">Высота</param>
    public SizeKey(int thick, int width, int height)
    {
        ThickInMm = thick;
        WidthInMm = width;
        HeightInMm = height;
    }

    /// <summary>
    /// Сравнивает данный объект с другим объектом
    /// </summary>
    public override bool Equals(object obj)
    {
        return obj is SizeKey dimensions && Equals(dimensions);
    }

    /// <summary>
    /// Сравнивает данный экземпляр с другим экземпляром
    /// </summary>
    public bool Equals(SizeKey other)
    {
        return ThickInMm == other.ThickInMm &&
               WidthInMm == other.WidthInMm &&
               HeightInMm == other.HeightInMm;
    }

    /// <summary>
    /// Получает хеш-код для текущего экземпляра
    /// </summary>
    public override int GetHashCode()
    {
        return Convert.ToInt32((ThickInMm * 1000) + (WidthInMm * 100) + (HeightInMm * 10));
    }

    /// <summary>
    /// Возвращает строковое представление размеров
    /// </summary>
    public override string ToString()
    {
        return $"{ThickInMm}x{WidthInMm}x{HeightInMm}";
    }

}