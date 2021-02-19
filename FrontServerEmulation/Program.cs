using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CachingFramework.Redis;
using CachingFramework.Redis.Contracts.Providers;
using StackExchange.Redis;
using FrontServerEmulation.Services;

namespace FrontServerEmulation
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //CreateHostBuilder(args).Build().Run();

            var host = CreateHostBuilder(args).Build();

            var monitorLoop = host.Services.GetRequiredService<MonitorLoop>();
            monitorLoop.StartMonitorLoop();

            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    try
                    {
                        //ConnectionMultiplexer muxer = ConnectionMultiplexer.Connect("redis");
                        ConnectionMultiplexer muxer = ConnectionMultiplexer.Connect("localhost");
                        services.AddSingleton<ICacheProviderAsync>(new RedisContext(muxer).Cache);                        
                        services.AddSingleton<IKeyEventsProvider>(new RedisContext(muxer).KeyEvents);
                    }
                    catch (Exception ex)
                    {
                        string message = ex.Message;
                        Console.WriteLine($"\n\n Redis server did not start: \n + {message} \n");
                        throw;
                    }

                    services.AddSingleton<ISettingConstants, SettingConstants>();
                    //services.AddHostedService<QueuedHostedService>();
                    //services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
                    services.AddSingleton<MonitorLoop>();
                    //services.AddSingleton<IBackgroundTasksService, BackgroundTasksService>();
                    services.AddSingleton<IOnKeysEventsSubscribeService, OnKeysEventsSubscribeService>();
                    services.AddSingleton<IFrontServerEmulationService, FrontServerEmulationService>();

                });
    }
}

// appsettings sharing between many solutions
//var settingPath = Path.GetFullPath(Path.Combine(@"../../appsettings.json")); // get absolute path
//var builder = new ConfigurationBuilder()
//        .SetBasePath(env.ContentRootPath)
//        .AddJsonFile(settingPath, optional: false, reloadOnChange: true);