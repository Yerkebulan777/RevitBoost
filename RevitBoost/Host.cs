using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Reflection;


namespace RevitBoost
{
    /// <summary>
    ///     ������������� ���� ��� �������� ���������� � ��������� �� ��������� ������
    /// </summary>
    public static class Host
    {
        private static IHost _host;

        /// <summary>
        ///  ��������� ���� � ����������� ������� ����������
        /// </summary>
        public static void Start()
        {
            try
            {
                // �������� �������� ����������� �����
                var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
                {
                    ContentRootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly()!.Location),
                    DisableDefaults = true
                });

                // ����������� ��������� �����������
                builder.Services.AddLogging(config =>
                {
                    config.ClearProviders();
                    config.AddSimpleConsole(options =>
                    {
                        options.SingleLine = true;
                    });
                });

                // ���������� � ������ �����
                _host = builder.Build();
                _host.Start();

                // ����������� ����������� �������������� ����������
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            }
            catch (Exception ex)
            {
                // ������ � Debug Output � ������ ������
                Debug.WriteLine($"����������� ������ ��� ������� �����: {ex}");
                throw;
            }
        }

        /// <summary>
        ///     ������������� ����
        /// </summary>
        public static void Stop()
        {
            try
            {
                _host?.StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"������ ��� ��������� �����: {ex}");
            }
        }

        /// <summary>
        ///     �������� ������ ���� <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T">��� �������������� �������</typeparam>
        public static T GetService<T>() where T : class
        {
            return _host.Services.GetRequiredService<T>();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            var exception = (Exception)args.ExceptionObject;
            Debug.WriteLine($"�������������� ����������: {exception}");
        }

    }
}