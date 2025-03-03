namespace CommonUtils.Logging
{
    /// <summary>
    /// Статический класс для управления логированием в библиотеках
    /// </summary>
    public static class LogManager
    {
        /// <summary>
        /// Текущий экземпляр логгера
        /// </summary>
        public static ILogger Current { get; private set; } = new SilentLogger();

        /// <summary>
        /// Устанавливает реализацию логгера
        /// </summary>
        public static void SetLogger(ILogger logger)
        {
            Current = logger ?? new SilentLogger();
        }

        /// <summary>
        /// Класс-заглушка для логирования, который ничего не делает
        /// </summary>
        private class SilentLogger : ILogger
        {
            public void Debug(string message)
            {
                System.Diagnostics.Debug.WriteLine(message);
            }

            public void Information(string message)
            {
                System.Diagnostics.Debug.WriteLine(message);
            }

            public void Warning(string message)
            {
                System.Diagnostics.Debug.WriteLine(message);
            }

            public void Error(string message)
            {
                System.Diagnostics.Debug.WriteLine(message);
            }

            public void Fatal(Exception exception, string message)
            {
                System.Diagnostics.Debug.WriteLine(exception, message);
            }

            public void CloseAndFlush()
            {

            }
        }
    }
}