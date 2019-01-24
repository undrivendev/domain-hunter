﻿using Mds.Common.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.IO;

namespace DomainHunter.Console
{
    class Program
    {
        static int Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
           .SetBasePath(Directory.GetCurrentDirectory())
           .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
           .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
           .AddEnvironmentVariables()
           .Build();

            Serilog.Log.Logger = new Serilog.LoggerConfiguration()
               .ReadFrom.Configuration(configuration)
               .CreateLogger();

            try
            {
                Mds.Common.Logging.ILogger logger = new SerilogLoggingProxy(Serilog.Log.Logger);


                 
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
