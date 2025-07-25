﻿using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.IO;

namespace RevitBoost.Config
{
    public static class ServiceConfiguration
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string logDirectory = Path.Combine(localAppData, "RevitBoost");

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    Path.Combine(logDirectory, "general.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 5,
                    shared: true)
                .CreateLogger();

            return services;
        }



    }
}