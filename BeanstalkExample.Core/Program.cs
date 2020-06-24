using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BeanstalkExample
{
    public class Program
    {
        private const string RunAsServiceFlag = "--service";
        public static async Task Main(string[] args)
        {
            if (args.Contains(RunAsServiceFlag))
            {
                args = args.Where(a => a != RunAsServiceFlag).ToArray();
                await RunAsService(args);
            }
            else
            {
                await RunInteractive(args);
            }
        }
        private static Task RunInteractive(String[] args) => 
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .RunConsoleAsync();

        private static async Task RunAsService(String[] args)
        {
            var assemblyLocationFolder = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (string.Compare(Environment.CurrentDirectory, assemblyLocationFolder, StringComparison.OrdinalIgnoreCase) != 0)
            {
                Environment.CurrentDirectory = assemblyLocationFolder;
            }
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
        }
    }
}
