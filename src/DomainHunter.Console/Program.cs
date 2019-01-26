﻿using DomainHunter.BLL;
using DomainHunter.BLL.Whois;
using DomainHunter.DAL;
using Mds.Common.Base;
using Mds.Common.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

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
                //COMMON
                Mds.Common.Logging.ILogger logger = new SerilogLoggingProxy(Log.Logger);

                //AUTOMAPPER
                //automapper
                AutoMapper.Mapper.Initialize(cfg =>
                {
                    cfg.CreateMissingTypeMaps = false;
                    AutoMapperConfiguration.Add(cfg);
                });
                AutoMapper.Mapper.Configuration.AssertConfigurationIsValid();
                IMapper mapper = new AutomapperWrapper(AutoMapper.Mapper.Instance);

                //REPOSITORY
                PsqlParameters psqlParameters = new PsqlParameters(configuration.GetConnectionString("Main"));
                IDomainRepository domainRepository = new CachedDomainRepository(new PsqlDomainRepository(psqlParameters, mapper));

                //APP SERVICES
                var huntParameters = new DomainHunterParameters()
                {
                    Length = int.Parse(configuration["DomainLength"]),
                    SleepMs = int.Parse(configuration["DomainSleepMs"]),
                    Tld = configuration["DomainTld"]
                };

                ServerSelectorOptions selectorOptions = new ServerSelectorOptions()
                {
                    Servers = new string[] 
                    {
                        "whois.verisign-grs.com"
                    }
                };

                IRandomNumberGenerator randomNumberGenerator = new DefaultRandomNumberGenerator();
                IServerSelector serverSelector = new RandomServerSelector(
                    selectorOptions,
                    randomNumberGenerator
                    );
                IWhoisService whoisService = new DefaultWhoisService(logger, serverSelector);
                IDomainChecker domainNameChecker = new WhoisDomainChecker(logger, whoisService);
               
                IRandomNameGenerator randomNameGenerator = new DefaultRandomNameGenerator(randomNumberGenerator);
                              
                var service = new DomainHunterService(
                    logger,
                    domainNameChecker,
                    randomNameGenerator,
                    domainRepository,
                    huntParameters);


                logger.Log("Starting the hunt...");

                var concurrentTaskNumber = int.Parse(configuration["ConcurrentTaskNumber"]);
                StartJobConcurrently(concurrentTaskNumber, service).Wait();

#pragma warning disable CS0162 // Unreachable code detected
                return 0;
#pragma warning restore CS0162 // Unreachable code detected
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

        private static async Task StartJobConcurrently(int concurrentTaskNumber, DomainHunterService service)
        {
            var currentTasks = new List<Task>();
            for (int i = 0; i < concurrentTaskNumber; i++)
            {
                currentTasks.Add(service.HuntName());
            }
            while (currentTasks.Count > 0)
            {
                var task = await Task.WhenAny(currentTasks);
                currentTasks.Remove(task);
                currentTasks.Add(service.HuntName());
            }
        }
    }
}
