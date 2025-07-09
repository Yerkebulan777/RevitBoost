using Serilog;
using Serilog.Core;
using Serilog.Events;

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
                .MinimumLevel.Debug()
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .WriteTo.File(path: logPath, shared: true)
                .CreateLogger();

            return new ModuleLogger(commandLogger, commandName);
        }


    }
}