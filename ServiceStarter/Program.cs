using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Npgsql;

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
            await Task.Delay(5000);

            CreateDatabaseIfNotExists();

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
                FileName = @"C:\Program Files\PostgreSQL\16\bin\pg_ctl.exe",
                Arguments = @"start -D ""C:\Program Files\PostgreSQL\16\data""",
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

        private static void CreateDatabaseIfNotExists()
        {
            var connectionString = "Host=localhost;Username=postgres;Password=pass;"; // Adjust this as needed
            var databaseName = "db"; // Replace with your actual database name
            var newUser = "user";
            var newUserPassword = "pass";

            using (var connection = new NpgsqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    Console.WriteLine("Connected to PostgreSQL successfully as 'postgres'.");

                    // Check if user exists
                    using (var command = new NpgsqlCommand($"SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = '{newUser}'", connection))
                    {
                        var exists = command.ExecuteScalar() != null;
                        if (!exists)
                        {
                            // Create user if it doesn't exist
                            Console.WriteLine($"Creating user '{newUser}'...");
                            using (var createUserCommand = new NpgsqlCommand($"CREATE ROLE \"{newUser}\" WITH LOGIN PASSWORD '{newUserPassword}';", connection))
                            {
                                createUserCommand.ExecuteNonQuery();
                                Console.WriteLine($"User '{newUser}' created successfully.");
                            }
                        }
                        else
                        {
                            // Update password for existing user
                            Console.WriteLine($"User '{newUser}' already exists. Updating password...");
                            using (var updateUserPasswordCommand = new NpgsqlCommand($"ALTER ROLE \"{newUser}\" WITH PASSWORD '{newUserPassword}';", connection))
                            {
                                updateUserPasswordCommand.ExecuteNonQuery();
                                Console.WriteLine($"User '{newUser}' password updated successfully.");
                            }
                        }
                    }

                    // Create database if not exists
                    using (var command = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{databaseName}'", connection))
                    {
                        var exists = command.ExecuteScalar() != null;
                        if (!exists)
                        {
                            Console.WriteLine($"Database '{databaseName}' does not exist. Creating...");
                            using (var createCommand = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\" OWNER \"{newUser}\"", connection))
                            {
                                createCommand.ExecuteNonQuery();
                            }
                            Console.WriteLine($"Database '{databaseName}' created successfully.");
                        }
                        else
                        {
                            Console.WriteLine($"Database '{databaseName}' already exists.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to check/create database or user. Error: {ex.Message}");
                }
            }
        }

        private static void ImportSqlFile()
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = @"C:\Program Files\PostgreSQL\16\bin\psql.exe",
                Arguments = @"-U user -d db -w -h localhost -f ""C:\Program Files\tidybee-hub\bin\init-hub-postgres.sql""",
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
                await Task.Delay(5000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start {serviceName}. Error: {ex.Message}");
            }
        }
    }
}


// using System;
// using System.Diagnostics;
// using System.IO;
// using System.Threading.Tasks;
// using Microsoft.Extensions.Configuration;

// namespace ServiceStarter
// {
//     class Program
//     {
//         static IConfiguration? Configuration { get; set; }

//         static async Task Main(string[] args)
//         {
//             var builder = new ConfigurationBuilder()
//                             .SetBasePath(AppContext.BaseDirectory)
//                             .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
//                             .AddEnvironmentVariables();
//             Configuration = builder.Build();

//             var isProduction = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Production";

//             var authPath = isProduction ? 
//                 Path.Combine(AppContext.BaseDirectory, "Auth", "Auth.exe") :
//                 Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\Auth\bin\Debug\net7.0\win-x64\Auth.exe"));
//             var dataProcessingPath = isProduction ? 
//                 Path.Combine(AppContext.BaseDirectory, "DataProcessing", "DataProcessing.exe") :
//                 Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\DataProcessing\bin\Debug\net7.0\win-x64\DataProcessing.exe"));
//             var apiGatewayPath = isProduction ? 
//                 Path.Combine(AppContext.BaseDirectory, "ApiGateway", "ApiGateway.exe") :
//                 Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\ApiGateway\bin\Debug\net7.0\win-x64\ApiGateway.exe"));
//             var tidyEventsPath = isProduction ? 
//                 Path.Combine(AppContext.BaseDirectory, "TidyEvents", "TidyEvents.exe") :
//                 Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\TidyEvents\bin\Debug\net8.0\win-x64\TidyEvents.exe"));

//             StartPostgreSQL();
//             await Task.Delay(5000);

//             var authTask = TryStartServiceAsync("Auth", authPath);
//             var dataProcessingTask = TryStartServiceAsync("DataProcessing", dataProcessingPath);
//             var apiGatewayTask = TryStartServiceAsync("ApiGateway", apiGatewayPath);
//             var tidyEventsTask = TryStartServiceAsync("TidyEvents", tidyEventsPath);

//             await Task.WhenAll(authTask, dataProcessingTask, apiGatewayTask, tidyEventsTask);

//             Console.WriteLine("All services have been started. Press any key to exit...");
//             Console.ReadKey();
//         }

//         private static void StartPostgreSQL()
//         {
//             var processStartInfo = new ProcessStartInfo
//             {
//                 FileName = @"C:\Program Files\PostgreSQL\16\bin\pg_ctl.exe",
//                 Arguments = @"start -D ""C:\Program Files\PostgreSQL\16\data""",
//                 UseShellExecute = false,
//                 RedirectStandardOutput = true,
//                 RedirectStandardError = true
//             };

//             using (var process = new Process { StartInfo = processStartInfo })
//             {
//                 try
//                 {
//                     process.Start();
//                     process.WaitForExit();
//                     Console.WriteLine("PostgreSQL started successfully.");

//                     // Import the SQL file
//                     ImportSqlFile();
//                 }
//                 catch (Exception ex)
//                 {
//                     Console.WriteLine($"Failed to start PostgreSQL. Error: {ex.Message}");
//                 }
//             }
//         }

//         private static void ImportSqlFile()
//         {
//             var processStartInfo = new ProcessStartInfo
//             {
//                 FileName = @"C:\Program Files\PostgreSQL\16\bin\psql.exe",
//                 Arguments = @"-U user -d db -w -h localhost -f ""C:\Program Files\tidybee-hub\bin\init-hub-postgres.sql""",
//                 UseShellExecute = false,
//                 RedirectStandardOutput = true,
//                 RedirectStandardError = true
//             };

//             using (var process = new Process { StartInfo = processStartInfo })
//             {
//                 try
//                 {
//                     process.Start();
//                     process.WaitForExit();
//                     Console.WriteLine("SQL file imported successfully.");
//                 }
//                 catch (Exception ex)
//                 {
//                     Console.WriteLine($"Failed to import SQL file. Error: {ex.Message}");
//                 }
//             }
//         }

//         private static async Task TryStartServiceAsync(string serviceName, string exePath)
//         {
//             if (!File.Exists(exePath))
//             {
//                 Console.WriteLine($"Executable path for {serviceName} does not exist: {exePath}");
//                 return;
//             }

//             Console.WriteLine($"Attempting to start {serviceName} from {exePath}...");
//             await StartServiceAsync(serviceName, exePath);
//         }

//         private static async Task StartServiceAsync(string serviceName, string exePath)
//         {
//             Console.WriteLine($"Starting {serviceName} from {exePath}...");
//             var processStartInfo = new ProcessStartInfo
//             {
//                 FileName = exePath,
//                 WorkingDirectory = Path.GetDirectoryName(exePath),
//                 UseShellExecute = false,
//                 RedirectStandardOutput = true,
//                 RedirectStandardError = true
//             };

//             var process = new Process { StartInfo = processStartInfo };

//             process.OutputDataReceived += (sender, e) => Console.WriteLine($"Output from {serviceName}: {e.Data}");
//             process.ErrorDataReceived += (sender, e) => Console.WriteLine($"Error output from {serviceName}: {e.Data}");

//             try
//             {
//                 process.Start();
//                 process.BeginOutputReadLine();
//                 process.BeginErrorReadLine();

//                 await Task.Run(() => process.WaitForExit());

//                 if (process.ExitCode == 0)
//                 {
//                     Console.WriteLine($"{serviceName} started successfully.");
//                 }
//                 else
//                 {
//                     Console.WriteLine($"{serviceName} exited with code {process.ExitCode}.");
//                 }
//                 await Task.Delay(5000);
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"Failed to start {serviceName}. Error: {ex.Message}");
//             }
//         }
//     }
// }
