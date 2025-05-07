using Microsoft.Playwright;
using CardGrabber.DAL;
using CardGrabber.Models;
using System.Runtime.InteropServices;
using System.Text.Json;
using CardGrabber.Configuration;
using CardGrabber.Services.Internal;
using CardGrabber.Services;
using CardGrabber.Services.Playwright;

namespace CardMarketScraper
{

    class Program
    {
        private static ConsoleEventDelegate _handler;
        private static bool isShuttingDown = false;

        private delegate bool ConsoleEventDelegate(CtrlType sig);

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate handler, bool add);

        private enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_CLOSE_EVENT = 2,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private static bool Handler(CtrlType signal)
        {
            if (!isShuttingDown)
            {
                isShuttingDown = true;
                Logger.OutputWarning("Shutdown signal received, performing final cleanup...", 0);
                RunManager runManager = new RunManager();
                runManager.CompleteRun(runID, "Stopped").GetAwaiter().GetResult();
            }

            return true;
        }

        #region Configurations
        public static string scraperUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";
        public static bool debugMode;
        public static bool onlyDBUsers;
        public static bool only1User;
        public static int runID = 0;
        #endregion


        static async Task Main(string[] args)
        {
            var config = ConfigurationLoader.Load();

            _handler = new ConsoleEventDelegate(Handler);
            SetConsoleCtrlHandler(_handler, true);

            AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
            {
                if (!isShuttingDown)
                {
                    isShuttingDown = true;
                    Logger.OutputWarning("Process is exiting... performing cleanup.", 0);
                    RunManager runManager = new RunManager();
                    await runManager.CompleteRun(runID,"Stopped");
                }
            };

            Console.CancelKeyPress += async (sender, e) =>
            {
                e.Cancel = true;
                if (!isShuttingDown)
                {
                    isShuttingDown = true;
                    Logger.OutputWarning("Ctrl+C pressed, cleaning up...", 0);
                    RunManager runManager = new RunManager();
                    await runManager.CompleteRun(runID, "Stopped");
                    Environment.Exit(0);
                }
            };

       

            debugMode = config.AppMode.DebugMode;
            onlyDBUsers = config.AppMode.OnlyDBUsers;
            only1User = config.AppMode.Only1User;

            int intervalInMinutes = 1;
            int intervalInMilliseconds = intervalInMinutes * 60 * 1000;

            while (true)
            {
                // STARTING CREATING A NEW RUN
                PlaywrightManager playwrightManager = new PlaywrightManager();

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
                RunManager runManager = new RunManager();

                Run run = null;
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

                string startupMessage = $"CardMarketGrabber started at {run.Start}. RunId: {run.RunId} RunIdentifier: {run.RunIdentifier}";
                Logger.OutputOk(startupMessage, runID);

                TelegramManager telegramManager = new TelegramManager();
                telegramManager.SendNotification(startupMessage);
                Logger.OutputOk($"Sent a notification on Telegram\n", 0);


                var dataAccess = new dal();

                // GET CARDS STATS
                Logger.OutputCustom("┌────────────────────────────────────────────────────┐", ConsoleColor.Magenta, runID);
                Logger.OutputCustom("│              CARD LOADING START                    │", ConsoleColor.Magenta, runID);
                Logger.OutputCustom("└────────────────────────────────────────────────────┘", ConsoleColor.Magenta, runID);

                using var playwright = await Playwright.CreateAsync();
                await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = !debugMode,
                    SlowMo = 100
                });

                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
                    UserAgent = scraperUserAgent
                });

                List<Card> cards = await dataAccess.GetAllCardsAsync();

                for (int i = 0; i < cards.Count; i++)
                {
                    Card card = cards[i];

                    Logger.OutputInfo($"[{(i + 1).ToString().PadLeft(2, '0')}/{cards.Count}] {card.CardName}", runID);
                    List<CardsInfo> cardsInfos;

                    cardsInfos = await playwrightManager.LoadMoreAndGetCardInfoAsync(card, context);
                    await dataAccess.InsertCardInfo(runID, card.CardID, JsonSerializer.Serialize(cardsInfos).ToString());

                    Logger.OutputInfo("Waiting 5 seconds before next card...\n", runID);
                    await Task.Delay(5000);

                }


                // GET USER ON DB AN SITE ONE
                Logger.OutputCustom("┌────────────────────────────────────────────────────┐", ConsoleColor.Magenta, runID);
                Logger.OutputCustom("│              SELLER LOADING START                  │", ConsoleColor.Magenta, runID);
                Logger.OutputCustom("└────────────────────────────────────────────────────┘", ConsoleColor.Magenta, runID);

                Logger.OutputInfo($"Starting to get sellers from DataBase", runID);
                IEnumerable<Sellers> dbSellers = await dataAccess.GetSellers();
                Logger.OutputOk($"Get {dbSellers.Count()} sellers from DataBase\n", runID);
                List<Sellers> siteSellers;
                siteSellers = new List<Sellers>();
                if (!onlyDBUsers)
                {
                    Logger.OutputInfo($"Starting to get sellers from site", runID);
                    string listingUrlDoubleRare = "https://www.cardmarket.com/it/Pokemon/Products/Singles?idCategory=51&idExpansion=0&idRarity=199&sortBy=price_asc&perSite=20&site=6";
                    string listingUrlIllustrationRare = "https://www.cardmarket.com/it/Pokemon/Products/Singles?idCategory=51&idExpansion=0&idRarity=280&sortBy=price_asc&perSite=20&site=6";
                    string listingUrlUltraRare = "https://www.cardmarket.com/it/Pokemon/Products/Singles?idCategory=51&idExpansion=0&idRarity=54&sortBy=price_asc&perSite=20&site=6";
                    string listingUrlHoloRare = "https://www.cardmarket.com/it/Pokemon/Products/Singles?idCategory=51&idExpansion=0&idRarity=49&perSite=20"; // POPOLARI NON ORDINATE PER PREZZO!
                    siteSellers.AddRange(await playwrightManager.getSellersFromUrl(context ,listingUrlDoubleRare, "DoubleRare",run));
                    siteSellers.AddRange(await playwrightManager.getSellersFromUrl(context ,listingUrlIllustrationRare, "IllustrationRare", run));
                    siteSellers.AddRange(await playwrightManager.getSellersFromUrl(context ,listingUrlUltraRare, "UltraRare", run));
                    siteSellers.AddRange(await playwrightManager.getSellersFromUrl(context ,listingUrlHoloRare, "HoloRare", run));
                    siteSellers = siteSellers
                        .GroupBy(u => u.Username)
                        .Select(g => g.First())
                        .ToList();
                    Logger.OutputOk($"Get {siteSellers.Count()} sellers from site\n", runID);
                }

                var dbUsernames = new HashSet<string>(
                    dbSellers.Select(s => s.Username),
                    StringComparer.OrdinalIgnoreCase
                );

                List<Sellers> newSellers = new();
                List<Sellers> existingSellers = new();

                if (siteSellers.Any())
                {
                    newSellers = siteSellers
                        .Where(s => !dbUsernames.Contains(s.Username))
                        .ToList();

                    existingSellers = siteSellers
                        .Where(s => dbUsernames.Contains(s.Username))
                        .ToList();
                }
                else
                {
                    existingSellers = dbSellers.ToList();
                }

                if (only1User && existingSellers.Any())
                {
                    var random = new Random();
                    existingSellers = new List<Sellers> { existingSellers[random.Next(existingSellers.Count)] };
                }

                // SELLER INFO COLLECTING
                Logger.OutputCustom("┌────────────────────────────────────────────────────┐", ConsoleColor.Magenta, runID);
                Logger.OutputCustom("│         SELLER INFO COLLECTING START               │", ConsoleColor.Magenta, runID);
                Logger.OutputCustom("└────────────────────────────────────────────────────┘", ConsoleColor.Magenta, runID);



                await context.RouteAsync("**/*", async route =>
                {
                    var req = route.Request;
                    if (req.ResourceType == "stylesheet" || req.ResourceType == "image" || req.ResourceType == "font")
                        await route.AbortAsync();
                    else
                        await route.ContinueAsync();
                });

                for (int i = 0; i < newSellers.Count; i++)
                {
                    var seller = newSellers[i];
                    Logger.OutputInfo($"[{(i + 1).ToString().PadLeft(2, '0')}/{existingSellers.Count}] {seller.Username}", runID);
                    await playwrightManager.GetSellerInfoAsync(seller.Username, context, run);
                    List<SellerItemsInfo> itemStats = await playwrightManager.getSellerItemsInfo(seller, context, run);
                    Logger.OutputInfo("Waiting 5 seconds before next seller...\n", runID);
                    await Task.Delay(5000);

                }
                for (int i = 0; i < existingSellers.Count; i++)
                {
                    var seller = existingSellers[i];
                    Logger.OutputInfo($"[{(i + 1).ToString().PadLeft(2, '0')}/{existingSellers.Count}] {seller.Username}", runID);
                    await playwrightManager.GetSellerInfoAsync(seller.Username, context, run);
                    List<SellerItemsInfo> itemStats = await playwrightManager.getSellerItemsInfo(seller, context, run);
                    Logger.OutputInfo("Waiting 5 seconds before next seller...\n", runID);
                    await Task.Delay(5000);
                }


                telegramManager.SendNotification($"RunCompleted waiting for {intervalInMinutes} minute before next run...");
                runManager.CompleteRun(runID, "Completed");

                Logger.OutputCustom("┌────────────────────────────────────────────────────┐", ConsoleColor.Magenta, runID);
                Logger.OutputCustom("│      CARDMARKET GRABBER HAVE COMPLETED THE RUN!    │", ConsoleColor.Magenta, runID);
                Logger.OutputCustom("└────────────────────────────────────────────────────┘", ConsoleColor.Magenta, runID);
                Logger.OutputOk($"RunCompleted waiting for {intervalInMinutes} minute before next run...", runID);

                await Task.Delay(intervalInMilliseconds);
            }
        }



      

       

       

       

      

      

    }
}