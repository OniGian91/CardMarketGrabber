namespace CardGrabber.Configuration;

public class AppSettings
{
    public TelegramBotSettings TelegramBot { get; set; }
    public AppModeSettings AppMode { get; set; }
    public DatabaseSettings Database { get; set; }
}

public class TelegramBotSettings
{
    public string BotToken { get; set; }
    public string ChatId { get; set; }
}

public class AppModeSettings
{
    public bool DebugMode { get; set; }
    public bool OnlyDBUsers { get; set; }
    public bool Only1User { get; set; }
}

public class DatabaseSettings
{
    public string ConnectionString { get; set; }
}
