using Serilog;
using Serilog.Core;

namespace CommonUtils
{
    public static class LoggerHelper
    {
        /// <summary>
        /// Создает временный логгер для команды с отдельным файлом
        /// </summary>
        public static IModuleLogger CreateCommandLogger(string title, string commandName, string directory)
        {
            string logPath = Path.Combine(directory, $"{title}.log");

            if (!Directory.Exists(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }
            else if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }

            // Создаем логгер для команды с отдельным файлом
            Logger commandLogger = new LoggerConfiguration()
                .WriteTo.File(path: logPath, shared: true)
                .MinimumLevel.Debug()
                .CreateLogger();

            return new ModuleLogger(commandLogger, commandName);
        }



    }
}