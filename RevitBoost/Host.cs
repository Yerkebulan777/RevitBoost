using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Reflection;


namespace RevitBoost
{
    /// <summary>
    ///     Предоставляет хост для сервисов приложения и управляет их жизненным циклом
    /// </summary>
    public static class Host
    {
        private static IHost _host;

        /// <summary>
        ///  Запускает хост и настраивает сервисы приложения
        /// </summary>
        public static void Start()
        {
            try
            {
                // Создание базового построителя хоста
                var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
                {
                    ContentRootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly()!.Location),
                    DisableDefaults = true
                });

                // Минимальная настройка логирования
                builder.Services.AddLogging(config =>
                {
                    config.ClearProviders();
                    config.AddSimpleConsole(options =>
                    {
                        options.SingleLine = true;
                    });
                });

                // Построение и запуск хоста
                _host = builder.Build();
                _host.Start();

                // Регистрация обработчика необработанных исключений
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            }
            catch (Exception ex)
            {
                // Запись в Debug Output в случае ошибки
                Debug.WriteLine($"Критическая ошибка при запуске хоста: {ex}");
                throw;
            }
        }

        /// <summary>
        ///     Останавливает хост
        /// </summary>
        public static void Stop()
        {
            try
            {
                _host?.StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при остановке хоста: {ex}");
            }
        }

        /// <summary>
        ///     Получить сервис типа <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T">Тип запрашиваемого сервиса</typeparam>
        public static T GetService<T>() where T : class
        {
            return _host.Services.GetRequiredService<T>();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            var exception = (Exception)args.ExceptionObject;
            Debug.WriteLine($"Необработанное исключение: {exception}");
        }

    }
}