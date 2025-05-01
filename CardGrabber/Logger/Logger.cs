using Microsoft.Data.SqlClient;
using System.Data;
using Dapper;

public static class Logger
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
    private static readonly string _connectionString = "Data Source=localhost;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Connect Timeout=60;Encrypt=False;TrustServerCertificate=True;";

    public static void OutputOk(string message, int runId)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SUCCESS] {message}");
        Console.ResetColor();
        LogToDatabase(message, LogSeverity.Success, runId);
    }

    public static void OutputWarning(string message, int runId)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARNING] {message}");
        Console.ResetColor();
        LogToDatabase(message, LogSeverity.Warning, runId);

    }

    public static void OutputError(string message, int runId)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
        LogToDatabase(message, LogSeverity.Error, runId);

    }

    public static void OutputInfo(string message, int runId)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[INFO] {message}");
        Console.ResetColor();
        LogToDatabase(message, LogSeverity.Info, runId);

    }

    public static void OutputDebug(string message, int runId)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"[DEBUG] {message}");
        Console.ResetColor();
        LogToDatabase(message, LogSeverity.Debug, runId);
    }

    public static void OutputCustom(string message, ConsoleColor color, int runId)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
        LogToDatabase(message, LogSeverity.Custom, runId);
    }

    private static void LogToDatabase(string message, LogSeverity severity, int runId)
    {
        try
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string sql = @"
            INSERT INTO dbo.Logs (RunId, Severity, LogDate, Message)
            VALUES (@RunId, @Severity, @LogDate, @Message)";

            db.Execute(sql, new
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