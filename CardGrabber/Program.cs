using Microsoft.Playwright;
using CardGrabber.Models;
using CardGrabber.Configuration;
using CardGrabber.Services.Internal;
using CardGrabber.Services;
using CardGrabber.Services.Playwright;

namespace CardMarketScraper
{

    class Program
    {

        #region Configurations
        public static int runID = 0;
        #endregion


        static async Task Main(string[] args)
        {

            Logger.OutputCustom("┌────────────────────────────────────────────────────┐", ConsoleColor.Magenta, runID);
            Logger.OutputCustom("│        STARTUP CARDMARKET GRABBER                  │", ConsoleColor.Magenta, runID);
            Logger.OutputCustom("└────────────────────────────────────────────────────┘", ConsoleColor.Magenta, runID);
            Logger.OutputCustom(@"
        ┌────────────────────────────┐
        │                            │
        │        ▓▓▓▓▓▓▓▓▓▓▓         │
        │      ▓▓          ▓▓        │
        │    ▓▓    ●    ●    ▓▓      │
        │   ▓▓     ░░░░░░     ▓▓     │
        │    ▓▓     ▓▓▓▓     ▓▓      │
        │      ▓▓          ▓▓        │
        │        ▓▓▓▓▓▓▓▓▓▓          │
        │                            │
        │        P O K É M O N       │
        │                            │
        └────────────────────────────┘
", ConsoleColor.DarkBlue, 0);

            Logger.OutputInfo("CardMarketGrabber is starting...", 0);

            var config = ConfigurationLoader.Load();
            TelegramManager telegramManager = new TelegramManager(config);
            RunManager runManager = new RunManager(config);
            PlaywrightManager playwrightManager = new PlaywrightManager(config);

            int intervalInMinutes = 1;

            while (true)
            {
                try
                {
                    Run run = new Run();
                    try
                    {
                        run = await runManager.StartRun();
                        runID = run.RunId;
                    }
                    catch (Exception ex)
                    {
                        Logger.OutputError($"CardMarketGrabber error on start! Exception: {ex.Message}\n", 0);
                        return;
                    }

                    Logger.OutputOk($"CardMarketGrabber started at {run.Start}. RunId: {run.RunId} RunIdentifier: {run.RunIdentifier}", runID);
                    await telegramManager.SendNotification($"CardMarketGrabber started at {run.Start}. RunId: {run.RunId} RunIdentifier: {run.RunIdentifier}");

                    // SET UP PLAYWRIGHT
                    using var playwright = await Playwright.CreateAsync();
                    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = !config.AppMode.DebugMode,
                        SlowMo = 100
                    });

                    var context = await browser.NewContextAsync(new BrowserNewContextOptions
                    {
                        ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
                        UserAgent = config.PlaywrightConfig.scraperUserAgent
                    });

                    await context.RouteAsync("**/*", async route =>
                    {
                        var req = route.Request;
                        if (req.ResourceType == "stylesheet" || req.ResourceType == "image" || req.ResourceType == "font")
                            await route.AbortAsync();
                        else
                            await route.ContinueAsync();
                    });

                    if (config.AppStrategy.collectCardsInfo)
                    {
                        Logger.OutputCustom("┌────────────────────────────────────────────────────┐", ConsoleColor.Magenta, runID);
                        Logger.OutputCustom("│              CARD LOADING START                    │", ConsoleColor.Magenta, runID);
                        Logger.OutputCustom("└────────────────────────────────────────────────────┘", ConsoleColor.Magenta, runID);

                        await playwrightManager.GetCardsInfo(context, run);
                        await Task.Delay(30000);
                    }
                    else
                    {
                        Logger.OutputCustom("AppStrategy collectCardsInfo is false skipping card loading...\n", ConsoleColor.Yellow, runID);
                    }

                    List<Sellers> sellers = new List<Sellers>();

                    if (config.AppStrategy.collectSellersItems || config.AppStrategy.collectSellers)
                    {
                        Logger.OutputCustom("┌────────────────────────────────────────────────────┐", ConsoleColor.Magenta, runID);
                        Logger.OutputCustom("│              SELLER LOADING START                  │", ConsoleColor.Magenta, runID);
                        Logger.OutputCustom("└────────────────────────────────────────────────────┘", ConsoleColor.Magenta, runID);
                        List<Sellers> siteSellers = new List<Sellers>();
                        List<Sellers> dbSellers = new List<Sellers>();

                        if (config.AppStrategy.collectSellers)
                        {
                            siteSellers = await playwrightManager.CollectSellersFromSite(context, run);
                        }
                        if (config.AppStrategy.collectSellersItems)
                        {
                            dbSellers = await playwrightManager.GetSellersFromDB(context, run);
                        }
                        sellers = dbSellers
                             .Concat(siteSellers)
                             .DistinctBy(s => s.Username)
                             .ToList();
                        await Task.Delay(30000);
                    }
                    else
                    {
                        Logger.OutputCustom("AppStrategy collectSellers is false skipping sellers loading...\n", ConsoleColor.Yellow, runID);
                    }

                    if (config.AppStrategy.collectSellersItems && sellers.Any())
                    {
                        // SELLER INFO COLLECTING
                        Logger.OutputCustom("┌────────────────────────────────────────────────────┐", ConsoleColor.Magenta, runID);
                        Logger.OutputCustom("│         SELLER INFO COLLECTING START               │", ConsoleColor.Magenta, runID);
                        Logger.OutputCustom("└────────────────────────────────────────────────────┘", ConsoleColor.Magenta, runID);

                        for (int i = 0; i < sellers.Count; i++)
                        {
                            var seller = sellers[i];
                            Logger.OutputInfo($"[{(i + 1).ToString().PadLeft(2, '0')}/{sellers.Count}] {seller.Username}", runID);
                            await playwrightManager.CollectSellerItems(seller.Username, context, run);
                            List<SellerItemsInfo> itemStats = await playwrightManager.getSellerItemsInfo(seller, context, run);
                            Logger.OutputInfo("Waiting 5 seconds before next seller...\n", runID);
                            await Task.Delay(5000);

                        }
                    }
                    else
                    {
                        Logger.OutputCustom("AppStrategy collectSellersItems is false or there are no sellers skipping seller items loading...\n", ConsoleColor.Yellow, runID);
                    }

                    await telegramManager.SendNotification($"RunCompleted waiting for {intervalInMinutes} minute before next run...");
                    await runManager.CompleteRun(runID, "Completed");

                    Logger.OutputCustom("┌────────────────────────────────────────────────────┐", ConsoleColor.Magenta, runID);
                    Logger.OutputCustom("│      CARDMARKET GRABBER HAVE COMPLETED THE RUN!    │", ConsoleColor.Magenta, runID);
                    Logger.OutputCustom("└────────────────────────────────────────────────────┘", ConsoleColor.Magenta, runID);
                    Logger.OutputOk($"RunCompleted waiting for {intervalInMinutes} minute before next run...", runID);

                    await Task.Delay(intervalInMinutes * 60 * 1000);
                }
                catch (Exception ex)
                {
                    await runManager.CompleteRun(runID, $"Error");
                    Logger.OutputError($"{ex.Message}", runID);

                    await Task.Delay(intervalInMinutes * 60 * 1000);
                }
            }
        }
    }
}