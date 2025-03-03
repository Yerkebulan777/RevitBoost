namespace CommonUtils.Logging
{
    /// <summary>
    /// Интерфейс для абстрактного логирования
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Запись отладочного сообщения
        /// </summary>
        void Debug(string message);

        /// <summary>
        /// Запись информационного сообщения
        /// </summary>
        void Information(string message);

        /// <summary>
        /// Запись предупреждения
        /// </summary>
        void Warning(string message);

        /// <summary>
        /// Запись сообщения об ошибке
        /// </summary>
        void Error(string message);

        /// <summary>
        /// Запись сообщения об ошибке с исключением
        /// </summary>
        void Fatal(Exception exception, string message);

        /// <summary>
        /// Закрывает и освобождает ресурсы логгера, записывая все ожидающие записи
        /// </summary>
        void CloseAndFlush();
    }
}