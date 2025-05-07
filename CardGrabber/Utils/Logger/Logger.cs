using Microsoft.Data.SqlClient;
using System.Data;
using Dapper;
using CardGrabber.Configuration;

namespace CardGrabber.Services
{
    public class Logger
    {
        public enum LogSeverity
        {
            Success = 1,
            Warning = 2,
            Error = 3,
            Info = 4,
            Debug = 5,
            Custom = 6
        }

        private readonly string _connectionString;

        public Logger()
        {
            var config = ConfigurationLoader.Load();
            _connectionString = config.Database.ConnectionString;
        }

        public static void OutputOk(string message, int runId) =>
            new Logger().LogOk(message, runId).GetAwaiter().GetResult();

        public static void OutputWarning(string message, int runId) =>
            new Logger().LogWarning(message, runId).GetAwaiter().GetResult();

        public static void OutputError(string message, int runId) =>
            new Logger().LogError(message, runId).GetAwaiter().GetResult();

        public static void OutputInfo(string message, int runId) =>
            new Logger().LogInfo(message, runId).GetAwaiter().GetResult();

        public static void OutputDebug(string message, int runId) =>
            new Logger().LogDebug(message, runId).GetAwaiter().GetResult();

        public static void OutputCustom(string message, ConsoleColor color, int runId) =>
            new Logger().LogCustom(message, color, runId).GetAwaiter().GetResult();

        public async Task LogOk(string message, int runId) =>
            await LogAsync(message, LogSeverity.Success, runId, ConsoleColor.Green);

        public async Task LogWarning(string message, int runId) =>
            await LogAsync(message, LogSeverity.Warning, runId, ConsoleColor.Yellow);

        public async Task LogError(string message, int runId) =>
            await LogAsync(message, LogSeverity.Error, runId, ConsoleColor.Red);

        public async Task LogInfo(string message, int runId) =>
            await LogAsync(message, LogSeverity.Info, runId, ConsoleColor.Cyan);

        public async Task LogDebug(string message, int runId) =>
            await LogAsync(message, LogSeverity.Debug, runId, ConsoleColor.Magenta);

        public async Task LogCustom(string message, ConsoleColor color, int runId) =>
            await LogAsync(message, LogSeverity.Custom, runId, color);

        private async Task LogAsync(string message, LogSeverity severity, int runId, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            string logMessage = severity == LogSeverity.Custom ? message : $"[{severity.ToString().ToUpper()}] {message}";
            Console.WriteLine(logMessage);
            Console.ResetColor();

            try
            {
                using IDbConnection db = new SqlConnection(_connectionString);
                string sql = @"
INSERT INTO CardGrabber.dbo.Logs (RunId, Severity, LogDate, Message)
VALUES (@RunId, @Severity, @LogDate, @Message);";

                await db.ExecuteAsync(sql, new
                {
                    RunId = runId,
                    Severity = (byte)severity,
                    LogDate = DateTime.Now,
                    Message = message
                });
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"[FATAL] Failed to log to database: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}
