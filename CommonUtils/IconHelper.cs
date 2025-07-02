using System.Windows.Media.Imaging;

namespace CommonUtils
{
    public static class IconHelper
    {
        private static readonly Dictionary<string, BitmapImage> IconCache = [];

        public static BitmapImage GetIcon(string iconName)
        {
            if (IconCache.TryGetValue(iconName, out BitmapImage cachedIcon))
            {
                return cachedIcon;
            }

            try
            {
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string resourceName = $"RevitBoost.Resources.Icons.{iconName}";

                using System.IO.Stream stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    // Логируем доступные ресурсы для отладки
                    string[] availableResources = assembly.GetManifestResourceNames();
                    System.Diagnostics.Trace.WriteLine($"Resource '{resourceName}' not found. Available resources:");
                    foreach (string resource in availableResources)
                    {
                        System.Diagnostics.Trace.WriteLine($"  - {resource}");
                    }
                    return null;
                }

                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();

                IconCache[iconName] = bitmap;
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error loading icon '{iconName}': {ex.Message}");
                return null;
            }
        }
    }
}
