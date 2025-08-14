using Fmr.Spark.InfoServer.Common.Cache;
using Fmr.Spark.InfoServer.Common.RabbitMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Fmr.StaticDataUpdater
{
    internal class Program
    {
        private static readonly AutoResetEvent _closing = new AutoResetEvent(false);
        static async Task Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                            .SetBasePath(AppContext.BaseDirectory)
                            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                            .Build();

            using IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                })
                .ConfigureServices((context, services) =>
                {
                    // Provide access to configuration and RabbitMQ details
                    services.AddSingleton<IConfiguration>(config);

                    // Connect to Redis
                    var multiplexer = ConnectionMultiplexer.Connect(config["RedisEndPoint"]);
                    services.AddSingleton<IConnectionMultiplexer>(multiplexer);

                    // Register IDatabase
                    services.AddSingleton(sp => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

                    // Register your RedisCacheService
                    services.AddTransient<RedisCacheService>();

                    // Register your background service
                    services.AddHostedService<StaticDataUpdaterService>();
                })
                .Build();



            await host.RunAsync();
        }
    }
}
