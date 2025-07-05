using Microsoft.Extensions.DependencyInjection;
using RevitBoost.Config;

namespace RevitBoost
{
    public static class Host
    {
        private static ServiceProvider _serviceProvider;

        public static void Start()
        {
            ServiceCollection services = new();
            services.ConfigureServices();

            _serviceProvider = services.BuildServiceProvider();
        }

        public static T GetService<T>()
        {
            return _serviceProvider.GetService<T>();
        }

        public static void Stop()
        {
            _serviceProvider?.Dispose();
        }
    }
}