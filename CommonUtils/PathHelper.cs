using System.Diagnostics;

namespace CommonUtils
{
    public static class PathHelper
    {
        // Найти папку PROJECT
        public static string FindProjectRoot(string currentPath)
        {
            string parentDir = Directory.GetParent(currentPath).FullName;
            string projectFolder = parentDir.FindFolder("*PROJECT*");
            Debug.Assert(!string.IsNullOrEmpty(projectFolder));
            return projectFolder;
        }

        // Найти папку по маске
        public static string FindFolder(this string path, string pattern)
        {
            return Directory.GetDirectories(path, pattern).FirstOrDefault();
        }

        // Создание директории, если не существует
        public static string EnsureDirectory(this string path)
        {
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            {
                _ = Directory.CreateDirectory(path);
            }

            return path;
        }

    }
}
