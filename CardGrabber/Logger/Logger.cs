using System;

public static class Logger
{
    // Output a success message in green
    public static void OutputOk(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SUCCESS] {message}");
        Console.ResetColor();
    }

    // Output a warning message in yellow
    public static void OutputWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARNING] {message}");
        Console.ResetColor();
    }

    // Output an error message in red
    public static void OutputError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
    }

    // Output an informational message in blue
    public static void OutputInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[INFO] {message}");
        Console.ResetColor();
    }

    // Output a debug message in gray
    public static void OutputDebug(string message)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"[DEBUG] {message}");
        Console.ResetColor();
    }

    // Output a custom message in the specified color
    public static void OutputCustom(string message, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }

}
