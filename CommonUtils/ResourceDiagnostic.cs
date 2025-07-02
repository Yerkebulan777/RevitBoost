using Autodesk.Revit.UI;
using System.Reflection;
using System.Text;

namespace CommonUtils
{
    public static class ResourceDiagnostic
    {
        /// <summary>
        /// Показывает ВСЮ правду о ресурсах в вашей сборке
        /// Этот метод поможет нам понять, что именно происходит
        /// </summary>
        public static void ShowCompleteResourceInfo()
        {
            try
            {
                // Получаем текущую сборку - это ключевой момент
                Assembly currentAssembly = Assembly.GetExecutingAssembly();

                // Собираем всю диагностическую информацию
                string assemblyLocation = currentAssembly.Location;
                string assemblyName = currentAssembly.GetName().Name;
                string[] allResources = currentAssembly.GetManifestResourceNames();

                // Создаем подробный отчет
                string diagnosticReport = BuildDiagnosticReport(assemblyName, assemblyLocation, allResources);

                // Показываем отчет в диалоге (размер диалога может быть ограничен)
                TaskDialog dialog = new("Диагностика ресурсов")
                {
                    MainContent = diagnosticReport
                };
                _ = dialog.Show();

                // Также выводим в консоль разработчика для полного анализа
                System.Diagnostics.Debug.WriteLine("=== ПОЛНАЯ ДИАГНОСТИКА РЕСУРСОВ ===");
                System.Diagnostics.Debug.WriteLine(diagnosticReport);

            }
            catch (Exception ex)
            {
                _ = TaskDialog.Show("Ошибка диагностики", $"Произошла ошибка: {ex.Message}");
            }
        }

        /// <summary>
        /// Создает подробный отчет о состоянии ресурсов
        /// </summary>
        private static string BuildDiagnosticReport(string assemblyName, string assemblyLocation, string[] resources)
        {
            StringBuilder report = new();

            // Основная информация о сборке
            _ = report.AppendLine($"Имя сборки: {assemblyName}");
            _ = report.AppendLine($"Расположение: {assemblyLocation}");
            _ = report.AppendLine($"Всего ресурсов найдено: {resources.Length}");
            _ = report.AppendLine();

            if (resources.Length == 0)
            {
                _ = report.AppendLine("ПРОБЛЕМА: Ресурсы вообще не найдены!");
                _ = report.AppendLine("Это означает, что файлы не были встроены в сборку.");
                return report.ToString();
            }

            // Анализируем каждый ресурс
            _ = report.AppendLine("Список всех встроенных ресурсов:");
            for (int i = 0; i < resources.Length; i++)
            {
                string resource = resources[i];
                _ = report.AppendLine($"{i + 1}. {resource}");

                // Проверяем, похож ли этот ресурс на иконку
                if (IsLikelyIcon(resource))
                {
                    _ = report.AppendLine($"   ↳ Похож на иконку!");
                }
            }

            // Ищем конкретно наши иконки
            _ = report.AppendLine();
            _ = report.AppendLine("Поиск ваших иконок:");
            CheckForSpecificIcon(report, resources, "RibbonIcon16.png");
            CheckForSpecificIcon(report, resources, "RibbonIcon32.png");

            return report.ToString();
        }

        /// <summary>
        /// Проверяет, похож ли ресурс на иконку
        /// </summary>
        private static bool IsLikelyIcon(string resourceName)
        {
            string lowerName = resourceName.ToLower();
            return lowerName.Contains("icon") ||
                   lowerName.EndsWith(".png") ||
                   lowerName.EndsWith(".ico") ||
                   lowerName.EndsWith(".bmp");
        }

        /// <summary>
        /// Ищет конкретную иконку и анализирует результаты
        /// </summary>
        private static void CheckForSpecificIcon(StringBuilder report, string[] resources, string iconName)
        {
            string[] exactMatches = resources.Where(r => r.EndsWith(iconName)).ToArray();

            if (exactMatches.Length > 0)
            {
                _ = report.AppendLine($"✓ {iconName} найден!");
                foreach (string match in exactMatches)
                {
                    _ = report.AppendLine($"   Полное имя: {match}");
                }
            }
            else
            {
                _ = report.AppendLine($"✗ {iconName} НЕ найден");

                // Ищем похожие имена для подсказки
                string[] similarNames = resources.Where(r =>
                    r.Contains(iconName.Replace(".png", "")) ||
                    (r.Contains("16") && iconName.Contains("16")) ||
                    (r.Contains("32") && iconName.Contains("32"))
                ).ToArray();

                if (similarNames.Length > 0)
                {
                    _ = report.AppendLine($"   Но найдены похожие:");
                    foreach (string similar in similarNames)
                    {
                        _ = report.AppendLine($"   - {similar}");
                    }
                }
            }
        }
    }
}
