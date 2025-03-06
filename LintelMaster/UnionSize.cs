namespace LintelMaster;

/// <summary>
/// Структура для отслеживания объединений групп
/// </summary>
public class UnionSize
{
    private readonly Dictionary<SizeKey, int> rank;
    private readonly Dictionary<SizeKey, SizeKey> parent;

    /// <summary>
    /// Создает новую структуру для отслеживания объединений
    /// </summary>
    public UnionSize(List<SizeKey> keys)
    {
        parent = new Dictionary<SizeKey, SizeKey>(keys.Count);
        rank = new Dictionary<SizeKey, int>(keys.Count);

        foreach (SizeKey key in keys)
        {
            parent[key] = key;  // Каждый элемент изначально является корнем
            rank[key] = 0;      // Начальный ранг равен 0
        }
    }

    /// <summary>
    /// Находит корневой элемент для указанного ключа
    /// </summary>
    public SizeKey FindRoot(SizeKey key)
    {
        if (!parent[key].Equals(key))
        {
            parent[key] = FindRoot(parent[key]); // Сжатие пути
        }

        return parent[key];
    }

    /// <summary>
    /// Объединяет две группы и возвращает корневой элемент результирующей группы
    /// </summary>
    public SizeKey Union(SizeKey key1, SizeKey key2, Dictionary<SizeKey, int> groupSizes)
    {
        SizeKey root1 = FindRoot(key1);
        SizeKey root2 = FindRoot(key2);

        if (root1.Equals(root2))
        {
            return root1;
        }

        int firstGroupSize = GetGroupSize(root1, groupSizes);
        int secondGroupSize = GetGroupSize(root2, groupSizes);

        // Всегда делаем корнем большую группу
        if (firstGroupSize < secondGroupSize)
        {
            parent[root1] = root2;
            return root2;
        }
        else
        {
            parent[root2] = root1;
            return root1;
        }
    }

    /// <summary>
    /// Вычисляет текущий размер группы
    /// </summary>
    private int GetGroupSize(SizeKey rootKey, Dictionary<SizeKey, int> groupSizes)
    {
        int totalSize = 0;

        foreach (KeyValuePair<SizeKey, int> entry in groupSizes)
        {
            if (FindRoot(entry.Key).Equals(rootKey))
            {
                totalSize += entry.Value;
            }
        }

        return totalSize;
    }



}