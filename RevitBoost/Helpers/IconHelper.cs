using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Media.Imaging;

namespace CommonUtils
{
    public static class IconHelper
    {
        // Точные имена ресурсов, как они существуют в сборке
        private const string SMALL_ICON_RESOURCE_NAME = "RevitBoost.Resources.Icons.RibbonIcon16.png";
        private const string LARGE_ICON_RESOURCE_NAME = "RevitBoost.Resources.Icons.RibbonIcon32.png";

        // Кэш для хранения загруженных иконок (чтобы не загружать их многократно)
        private static readonly Dictionary<string, BitmapImage> IconCache = [];

        /// <summary>
        /// Загружает иконку из embedded ресурса по точному имени
        /// Этот метод работает как точный адрес - мы знаем, где искать
        /// </summary>
        private static BitmapImage GetIconFromEmbeddedResource(string resourceName)
        {
            // Сначала проверяем кэш - это экономит время и ресурсы
            if (IconCache.TryGetValue(resourceName, out BitmapImage cachedIcon))
            {
                System.Diagnostics.Debug.WriteLine($"Иконка '{resourceName}' загружена из кэша");
                return cachedIcon;
            }

            try
            {
                // Получаем текущую сборку - это наша "библиотека" с ресурсами
                Assembly currentAssembly = Assembly.GetExecutingAssembly();

                // Проверяем, существует ли ресурс с таким именем
                string[] availableResources = currentAssembly.GetManifestResourceNames();
                if (!availableResources.Contains(resourceName))
                {
                    System.Diagnostics.Debug.WriteLine($"Ресурс '{resourceName}' не найден в сборке");
                    System.Diagnostics.Debug.WriteLine("Доступные ресурсы:");
                    foreach (string resource in availableResources)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - {resource}");
                    }
                    return null;
                }

                // Загружаем ресурс из сборки
                using Stream resourceStream = currentAssembly.GetManifestResourceStream(resourceName);
                if (resourceStream == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Не удалось открыть поток для ресурса '{resourceName}'");
                    return null;
                }

                // Создаем BitmapImage из потока
                // Это как превращение цифрового кода обратно в изображение
                BitmapImage bitmap = new();
                bitmap.BeginInit();

                // Важная настройка: загружаем изображение полностью в память
                // Это предотвращает проблемы с блокировкой потока
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = resourceStream;

                bitmap.EndInit();

                // Замораживаем объект для оптимизации и безопасности многопоточности
                bitmap.Freeze();

                System.Diagnostics.Debug.WriteLine($"Успешно загружена иконка '{resourceName}' ({bitmap.PixelWidth}x{bitmap.PixelHeight})");

                // Сохраняем в кэш для будущих обращений
                IconCache[resourceName] = bitmap;

                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки иконки '{resourceName}': {ex.Message}");

                // Сохраняем информацию об ошибке в кэш, чтобы не пытаться загружать повторно
                IconCache[resourceName] = null;

                return null;
            }
        }

        /// <summary>
        /// Получает маленькую иконку (16x16 пикселей)
        /// Использует точное имя ресурса, которое мы определили в анализе
        /// </summary>
        public static BitmapImage GetSmallIcon()
        {
            return GetIconFromEmbeddedResource(SMALL_ICON_RESOURCE_NAME);
        }

        /// <summary>
        /// Получает большую иконку (32x32 пикселя)
        /// Предполагаем, что она имеет аналогичное имя
        /// </summary>
        public static BitmapImage GetLargeIcon()
        {
            return GetIconFromEmbeddedResource(LARGE_ICON_RESOURCE_NAME);
        }

        /// <summary>
        /// Диагностический метод для проверки доступности обеих иконок
        /// Полезно для отладки и подтверждения правильности работы
        /// </summary>
        public static string TestIconAvailability()
        {
            StringBuilder result = new();
            _ = result.AppendLine("=== ТЕСТ ДОСТУПНОСТИ ИКОНОК ===");

            // Тестируем маленькую иконку
            BitmapImage smallIcon = GetSmallIcon();
            _ = smallIcon != null
                ? result.AppendLine($"✅ Маленькая иконка: успешно загружена ({smallIcon.PixelWidth}x{smallIcon.PixelHeight})")
                : result.AppendLine("❌ Маленькая иконка: не удалось загрузить");

            // Тестируем большую иконку
            BitmapImage largeIcon = GetLargeIcon();
            _ = largeIcon != null
                ? result.AppendLine($"✅ Большая иконка: успешно загружена ({largeIcon.PixelWidth}x{largeIcon.PixelHeight})")
                : result.AppendLine("❌ Большая иконка: не удалось загрузить");

            return result.ToString();
        }
    }
}
