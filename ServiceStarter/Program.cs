using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace ServiceStarter
{
    class Program
    {
        static IConfiguration? Configuration { get; set; }

        static async Task Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                            .SetBasePath(AppContext.BaseDirectory)
                            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                            .AddEnvironmentVariables();
            Configuration = builder.Build();

            var isProduction = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Production";

            var authPath = isProduction ? 
                Path.Combine(AppContext.BaseDirectory, "Auth", "Auth.exe") :
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\Auth\bin\Debug\net7.0\win-x64\Auth.exe"));
            var dataProcessingPath = isProduction ? 
                Path.Combine(AppContext.BaseDirectory, "DataProcessing", "DataProcessing.exe") :
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\DataProcessing\bin\Debug\net7.0\win-x64\DataProcessing.exe"));
            var apiGatewayPath = isProduction ? 
                Path.Combine(AppContext.BaseDirectory, "ApiGateway", "ApiGateway.exe") :
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\ApiGateway\bin\Debug\net7.0\win-x64\ApiGateway.exe"));
            var tidyEventsPath = isProduction ? 
                Path.Combine(AppContext.BaseDirectory, "TidyEvents", "TidyEvents.exe") :
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\TidyEvents\bin\Debug\net8.0\win-x64\TidyEvents.exe"));

            StartPostgreSQL();

            var authTask = TryStartServiceAsync("Auth", authPath);
            var dataProcessingTask = TryStartServiceAsync("DataProcessing", dataProcessingPath);
            var apiGatewayTask = TryStartServiceAsync("ApiGateway", apiGatewayPath);
            var tidyEventsTask = TryStartServiceAsync("TidyEvents", tidyEventsPath);

            await Task.WhenAll(authTask, dataProcessingTask, apiGatewayTask, tidyEventsTask);

            Console.WriteLine("All services have been started. Press any key to exit...");
            Console.ReadKey();
        }

        private static void StartPostgreSQL()
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = @"C:\Program Files\PostgreSQL\bin\pg_ctl.exe",
                Arguments = @"start -D ""C:\Program Files\PostgreSQL\data""",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = new Process { StartInfo = processStartInfo })
            {
                try
                {
                    process.Start();
                    process.WaitForExit();
                    Console.WriteLine("PostgreSQL started successfully.");

                    // Import the SQL file
                    ImportSqlFile();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start PostgreSQL. Error: {ex.Message}");
                }
            }
        }

        private static void ImportSqlFile()
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = @"C:\Program Files\PostgreSQL\bin\psql.exe",
                Arguments = @"-U user -d db -p pass -f ""C:\Program Files\tidybee-hub\bin\init-hub-postgres.sql""",
                // mettre  user db et pass depuis la config
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = new Process { StartInfo = processStartInfo })
            {
                try
                {
                    process.Start();
                    process.WaitForExit();
                    Console.WriteLine("SQL file imported successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to import SQL file. Error: {ex.Message}");
                }
            }
        }

        private static async Task TryStartServiceAsync(string serviceName, string exePath)
        {
            if (!File.Exists(exePath))
            {
                Console.WriteLine($"Executable path for {serviceName} does not exist: {exePath}");
                return;
            }

            Console.WriteLine($"Attempting to start {serviceName} from {exePath}...");
            await StartServiceAsync(serviceName, exePath);
        }

        private static async Task StartServiceAsync(string serviceName, string exePath)
        {
            Console.WriteLine($"Starting {serviceName} from {exePath}...");
            var processStartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = new Process { StartInfo = processStartInfo };

            process.OutputDataReceived += (sender, e) => Console.WriteLine($"Output from {serviceName}: {e.Data}");
            process.ErrorDataReceived += (sender, e) => Console.WriteLine($"Error output from {serviceName}: {e.Data}");

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit());

                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"{serviceName} started successfully.");
                }
                else
                {
                    Console.WriteLine($"{serviceName} exited with code {process.ExitCode}.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start {serviceName}. Error: {ex.Message}");
            }
        }
    }
}
