using Microsoft.Win32;

namespace CommonUtils
{
    public static class PathHelper
    {
        /// <summary>
        /// Находит родительскую папку проекта по заданному пути
        /// </summary>
        public static string GetProjectDirectory(string path, string pattern = "*PROJECT*")
        {
            DirectoryInfo dirInfo = Directory.GetParent(path);
            string projectPath = dirInfo.FindFolder(pattern);
            return projectPath is null ? dirInfo.FullName : projectPath;
        }

        /// <summary>
        /// Найти папку по маске
        /// </summary>
        public static string FindFolder(this DirectoryInfo dirInfo, string pattern)
        {
            return Directory.GetDirectories(dirInfo.FullName, pattern).FirstOrDefault();
        }

        /// <summary>
        /// Создание директории, если не существует
        /// </summary>
        public static string EnsureDirectory(this string path)
        {
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }

        /// <summary>
        /// Получает UNC путь для сетевого ресурса
        /// </summary>
        public static string GetUNCPath(string inputPath)
        {
            inputPath = Path.GetFullPath(inputPath);

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Network\\" + inputPath[0]))
            {
                if (key != null)
                {
                    inputPath = key.GetValue("RemotePath").ToString() + inputPath.Remove(0, 2).ToString();
                }
            }

            return inputPath;
        }



    }
}
