using Autodesk.Revit.DB;
using CommonUtils;

namespace LintelMaster;

/// <summary>
/// Структура для хранения и сравнения ключевых размеров перемычки
/// </summary>
public readonly struct SizeKey : IEquatable<SizeKey>, IComparable<SizeKey>
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

        if (thick == 0 || width == 0 || height == 0)
        {
            throw new ArgumentException("Invalid lintel dimensions");
        }

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

    public int CompareTo(SizeKey other)
    {
        if (ThickInMm != other.ThickInMm)
        {
            return ThickInMm.CompareTo(other.ThickInMm);
        }

        if (WidthInMm != other.WidthInMm)
        {
            return WidthInMm.CompareTo(other.WidthInMm);
        }

        return HeightInMm.CompareTo(other.HeightInMm);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 10;
            hash = (hash * 50) + ThickInMm;
            hash = (hash * 30) + WidthInMm;
            hash = (hash * 20) + HeightInMm;
            return hash;
        }
    }

    /// <summary>
    /// Возвращает строковое представление размеров
    /// </summary>
    public override string ToString()
    {
        return $"{ThickInMm}x{WidthInMm}x{HeightInMm}";
    }



}