using Microsoft.Extensions.Configuration;

namespace CardGrabber.Configuration;

public static class ConfigurationLoader
{
    public static AppSettings Load()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var appSettings = new AppSettings
        {
            TelegramBot = configuration.GetSection("TelegramBot").Get<TelegramBotSettings>(),
            AppMode = configuration.GetSection("AppMode").Get<AppModeSettings>(),
            Database = configuration.GetSection("Database").Get<DatabaseSettings>(),
            AppStrategy = configuration.GetSection("AppStrategy").Get<AppStrategy>(),
            PlaywrightConfig = configuration.GetSection("PlaywrightConfig").Get<PlaywrightConfig>()
        }; 
        
        return appSettings;
    }
}
