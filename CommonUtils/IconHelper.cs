using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace CommonUtils
{
    public static class IconHelper
    {
        // Кэшируем как успешные, так и неудачные попытки загрузки
        private static readonly Dictionary<string, BitmapImage> _iconCache = [];

        // Кэшируем информацию о сборке для избежания повторных обращений
        private static readonly Lazy<IconAssemblyInfo> _assemblyInfo = new(
            () => new IconAssemblyInfo(Assembly.GetExecutingAssembly())
        );

        /// <summary>
        /// Получает иконку по имени. Использует ленивую загрузку и кэширование.
        /// </summary>
        public static BitmapImage GetIcon(string iconName)
        {
            // Проверяем кэш - это самая быстрая операция
            if (_iconCache.TryGetValue(iconName, out BitmapImage cachedIcon))
            {
                return cachedIcon;
            }

            // Используем кэшированную информацию о сборке
            IconAssemblyInfo assemblyInfo = _assemblyInfo.Value;

            // Ищем ресурс по оптимизированному алгоритму
            string resourceName = FindResourceName(assemblyInfo, iconName);

            BitmapImage result = null;
            if (resourceName != null)
            {
                result = LoadBitmapFromResource(assemblyInfo.Assembly, resourceName);
            }

            // Кэшируем результат (даже если null, чтобы не искать повторно)
            _iconCache[iconName] = result;

            return result;
        }

        /// <summary>
        /// Находит точное имя ресурса в сборке используя оптимизированный поиск
        /// </summary>
        private static string FindResourceName(IconAssemblyInfo assemblyInfo, string iconName)
        {
            HashSet<string> resources = assemblyInfo.ResourceNames;

            // Стратегия поиска: от наиболее вероятного к наименее вероятному

            // 1. Точное совпадение с ожидаемым паттерном (наиболее частый случай)
            string expectedName = $"{assemblyInfo.AssemblyName}.Resources.Icons.{iconName}";
            if (resources.Contains(expectedName))
            {
                return expectedName;
            }

            // 2. Поиск по окончанию (второй по частоте случай)
            string endingMatch = resources.FirstOrDefault(r => r.EndsWith(iconName, StringComparison.OrdinalIgnoreCase));
            if (endingMatch != null)
            {
                return endingMatch;
            }

            // 3. Только если предыдущие методы не сработали - показываем диагностику
            LogDiagnosticInfo(assemblyInfo, iconName);

            return null;
        }

        /// <summary>
        /// Загружает bitmap из ресурса сборки с оптимальными настройками
        /// </summary>
        private static BitmapImage LoadBitmapFromResource(Assembly assembly, string resourceName)
        {
            try
            {
                using Stream stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    return null;
                }

                BitmapImage bitmap = new();
                bitmap.BeginInit();

                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;

                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load resource {resourceName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Выводит диагностическую информацию только при необходимости
        /// </summary>
        private static void LogDiagnosticInfo(IconAssemblyInfo assemblyInfo, string iconName)
        {
            Debug.WriteLine($"Icon '{iconName}' not found. Available resources:");

            // Показываем только ресурсы, которые могут быть иконками
            IOrderedEnumerable<string> iconResources = assemblyInfo.ResourceNames
                .Where(r => r.Contains("Icon") || r.EndsWith(".png") || r.EndsWith(".ico"))
                .OrderBy(r => r);

            foreach (string resource in iconResources)
            {
                Debug.WriteLine($"  {resource}");
            }
        }
    }

    /// <summary>
    /// Вспомогательный класс для кэширования информации о сборке
    /// Использует паттерн Lazy для инициализации только при необходимости
    /// </summary>
    internal class IconAssemblyInfo
    {
        public Assembly Assembly { get; }
        public string AssemblyName { get; }
        public HashSet<string> ResourceNames { get; }

        public IconAssemblyInfo(Assembly assembly)
        {
            AssemblyName = assembly.GetName().Name;
            Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            ResourceNames = new HashSet<string>(assembly.GetManifestResourceNames(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
