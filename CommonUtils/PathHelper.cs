namespace CommonUtils
{
    public static class PathHelper
    {
        // Найти папку PROJECT
        public static string FindProjectRoot(string path)
        {
            DirectoryInfo dirInfo = Directory.GetParent(path);
            string projectPath = dirInfo.FindFolder("*PROJECT*");
            return projectPath is null ? dirInfo.FullName : projectPath;
        }

        // Найти папку по маске
        public static string FindFolder(this DirectoryInfo dirInfo, string pattern)
        {
            return Directory.GetDirectories(dirInfo.FullName, pattern).FirstOrDefault();
        }

        // Создание директории, если не существует
        public static string EnsureDirectory(this string path)
        {
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }

    }
}
