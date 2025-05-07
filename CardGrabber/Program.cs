using Microsoft.Playwright;
using CardGrabber.DAL;
using CardGrabber.Classes;
using System.Runtime.InteropServices;
using System;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json;

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
                OnShutdown().GetAwaiter().GetResult();
            }

            return true;
        }

        #region Configurations
        public static bool debugMode;
        public static bool onlyDBUsers;
        public static bool only1User;
        public static int runID = 0;
        public static string scraperUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";
        #endregion


        static async Task Main(string[] args)
        {

            _handler = new ConsoleEventDelegate(Handler);
            SetConsoleCtrlHandler(_handler, true);

            AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
            {
                if (!isShuttingDown)
                {
                    isShuttingDown = true;
                    Logger.OutputWarning("Process is exiting... performing cleanup.", 0);
                    await OnShutdown();
                }
            };

            Console.CancelKeyPress += async (sender, e) =>
            {
                e.Cancel = true;
                if (!isShuttingDown)
                {
                    isShuttingDown = true;
                    Logger.OutputWarning("Ctrl+C pressed, cleaning up...", 0);
                    await OnShutdown();
                    Environment.Exit(0);
                }
            };

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            string botToken = configuration["TelegramBot:BotToken"];
            string chatId = configuration["TelegramBot:ChatId"];

            // Access AppMode settings
            debugMode = bool.Parse(configuration["AppMode:debugMode"]);
            onlyDBUsers = bool.Parse(configuration["AppMode:onlyDBUsers"]);
            only1User = bool.Parse(configuration["AppMode:only1User"]);


            int intervalInMinutes = 1;
            int intervalInMilliseconds = intervalInMinutes * 60 * 1000;

            while (true)
            {
                // STARTING CREATING A NEW RUN

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
                Run run = null;
                try
                {
                    run = await StartAndCollectNewRun();
                }
                catch (Exception ex)
                {
                    Logger.OutputError($"CardMarketGrabber error on start! Exception: {ex.Message}\n", 0);
                    return;
                }
                runID = run.RunId;
                string startupMessage = $"CardMarketGrabber started at {run.Start}. RunId: {run.RunId} RunIdentifier: {run.RunIdentifier}";
                Logger.OutputOk(startupMessage, runID);

                // TELEGRAM
                using HttpClient client = new HttpClient();
                HttpResponseMessage startupResponse = await client.GetAsync($"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(startupMessage)}");

                if (startupResponse.IsSuccessStatusCode)
                    Logger.OutputOk("Sent a notification on Telegram", runID);
                else
                    Logger.OutputError("Failed to send a notification on Telegram", runID);

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

                    cardsInfos = await LoadMoreAndGetCardInfoAsync(card, context);
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
                    siteSellers.AddRange(await getSellersFromUrl(listingUrlDoubleRare, "DoubleRare"));
                    siteSellers.AddRange(await getSellersFromUrl(listingUrlIllustrationRare, "IllustrationRare"));
                    siteSellers.AddRange(await getSellersFromUrl(listingUrlUltraRare, "UltraRare"));
                    siteSellers.AddRange(await getSellersFromUrl(listingUrlHoloRare, "HoloRare"));
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
                    await GetSellerInfoAsync(seller.Username, context, run);
                    List<SellerItemsInfo> itemStats = await getItemsNumV2(seller, context, run);
                    Logger.OutputInfo("Waiting 5 seconds before next seller...\n", runID);
                    await Task.Delay(5000);

                }
                for (int i = 0; i < existingSellers.Count; i++)
                {
                    var seller = existingSellers[i];
                    Logger.OutputInfo($"[{(i + 1).ToString().PadLeft(2, '0')}/{existingSellers.Count}] {seller.Username}", runID);
                    await GetSellerInfoAsync(seller.Username, context, run);
                    List<SellerItemsInfo> itemStats = await getItemsNumV2(seller, context, run);
                    Logger.OutputInfo("Waiting 5 seconds before next seller...\n", runID);
                    await Task.Delay(5000);
                }


                // TELEGRAM
                string completeRunMessage = $"RunCompleted waiting for {intervalInMinutes} minute before next run...";
                HttpResponseMessage completeResponse = await client.GetAsync($"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(completeRunMessage)}");

                if (completeResponse.IsSuccessStatusCode)
                    Logger.OutputOk("Sent a notification on Telegram", runID);
                else
                    Logger.OutputError("Failed to send a notification on Telegram", runID);
                // RUN COMPLETE
                await dataAccess.CompleteRun(runID, "Completed");
                Logger.OutputCustom("┌────────────────────────────────────────────────────┐", ConsoleColor.Magenta, runID);
                Logger.OutputCustom("│      CARDMARKET GRABBER HAVE COMPLETED THE RUN!    │", ConsoleColor.Magenta, runID);
                Logger.OutputCustom("└────────────────────────────────────────────────────┘", ConsoleColor.Magenta, runID);
                Logger.OutputOk(completeRunMessage, runID);

                await Task.Delay(intervalInMilliseconds);
            }
        }

        static async Task<Run> StartAndCollectNewRun()
        {
            Guid runId = Guid.NewGuid();
            var dataAccess = new dal();
            Run run = await dataAccess.CreateNewRun(runId);
            return run;
        }

        static async Task OnShutdown()
        {
            Logger.OutputWarning("Application is shutting down... performing cleanup.", 0);
            try
            {
                var dataAccess = new dal();
                await dataAccess.CompleteRun(runID, "Stopped");
            }
            catch (Exception ex)
            {
                Logger.OutputError($"Error during shutdown: {ex.Message}", 0);
            }
        }

        static async Task<List<Sellers>> getSellersFromUrl(string listingUrl, string type)
        {
            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = !debugMode,
                SlowMo = 100
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
                UserAgent = scraperUserAgent
            });

            await context.RouteAsync("**/*", async route =>
            {
                var req = route.Request;
                if (req.ResourceType == "stylesheet" || req.ResourceType == "image" || req.ResourceType == "font")
                    await route.AbortAsync();
                else
                    await route.ContinueAsync();
            });
            var page = await context.NewPageAsync();
            page.SetDefaultTimeout(5000);

            List<Sellers> usersList = new List<Sellers>();

            await page.GotoAsync(listingUrl);

            var productDivs = await page.Locator("div[id^='productRow']").AllAsync();
            for (int i = 0; i < productDivs.Count; i++)
            {
                var product = productDivs[i];

                try
                {
                    var productHandle = await product.ElementHandleAsync();
                    if (productHandle == null) continue;
                    var linkHandles = await productHandle.QuerySelectorAllAsync("a");
                    string productUrl = null;

                    foreach (var link in linkHandles)
                    {
                        var href = await link.GetAttributeAsync("href");
                        if (!string.IsNullOrEmpty(href) && href.Contains("/Singles/"))
                        {
                            productUrl = $"https://www.cardmarket.com{href}?sellerCountry=17&minCondition=3";
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(productUrl)) continue;

                    var response = await page.GotoAsync(productUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

                    if (response?.Status == 429 || (await page.ContentAsync()).Contains("HTTP ERROR 429"))
                    {
                        Logger.OutputWarning($"({type}) HTTP 429 Too Many Requests for product at index {i}. Waiting 30 sec and retrying...", runID);
                        await Task.Delay(TimeSpan.FromSeconds(30));

                        response = await page.GotoAsync(productUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

                        if (response?.Status == 429 || (await page.ContentAsync()).Contains("HTTP ERROR 429"))
                        {
                            Logger.OutputError($"({type}) Still receiving 429 after retry. Skipping this product.", runID);
                            continue;
                        }
                    }
                    var noResultsLocator = page.Locator("p.noResults");
                    if (await noResultsLocator.IsVisibleAsync())
                    {
                        Logger.OutputDebug($"({type}) No sellers found for product at index {i}. Skipping...", runID);
                        await page.GoBackAsync(new PageGoBackOptions { WaitUntil = WaitUntilState.Load });
                        continue;
                    }

                    await page.WaitForSelectorAsync("div.article-row");
                    var articleRows = await page.Locator("div.article-row").AllAsync();

                    var newUsers = 0;

                    foreach (var row in articleRows)
                    {
                        var priceLocator = row.Locator("div.price-container span.fw-bold");
                        var priceCount = await priceLocator.CountAsync();

                        if (priceCount > 0)
                        {
                            var sellerAnchor = row.Locator("span.seller-name a");
                            if (await sellerAnchor.CountAsync() > 0)
                            {
                                var username = await sellerAnchor.First.InnerTextAsync();
                                var seller = new Sellers { Username = username };
                                usersList.Add(seller);
                                newUsers++;
                            }
                        }
                    }

                    Logger.OutputOk($"({type}) Added: {newUsers} sellers to the list. Waiting 5 sec before next user...", runID);

                    await page.GoBackAsync(new PageGoBackOptions { WaitUntil = WaitUntilState.Load });
                }
                catch (Exception ex)
                {
                    Logger.OutputError($"({type}) Error processing product at index {i}: {ex.Message}", runID);
                }

                await Task.Delay(500);
            }

            await browser.CloseAsync();

            return usersList;
        }

        static async Task<List<SellerItemsInfo>> getItemsNumV2(Sellers seller, IBrowserContext context, Run run)
        {
            var page = await context.NewPageAsync();

            // Base URL for the seller's offers
            string baseUrl = $"https://www.cardmarket.com/it/Pokemon/Users/{seller.Username}/Offers/Singles?condition=3&sortBy=price_asc";

            // URLs for each rarity type
            var rarityUrls = new Dictionary<string, string>
    {
        { "DoubleRare", $"{baseUrl}&maxPrice=0.3&idRarity=199" },
        { "UltraRare", $"{baseUrl}&maxPrice=1&idRarity=54" },
        { "IllustrationRare", $"{baseUrl}&maxPrice=0.8&idRarity=280" },
        { "HoloRare", $"{baseUrl}&maxPrice=0.05&idRarity=4&isReverseHolo=N9" }
    };

            // List to collect item stats
            var itemStatsList = new List<SellerItemsInfo>();

            // Collect data for each rarity type
            foreach (var rarity in rarityUrls)
            {
                var (count, avgPrice) = await GetTotalCountFromUserPage(page, rarity.Value, seller.Username);
                itemStatsList.Add(new SellerItemsInfo(rarity.Key, count, avgPrice));
            }

            // Optionally store the data in the database
            var dataAccess = new dal();
            dataAccess.StoreSellerItemInfo(
                seller.Username,
                itemStatsList.First(i => i.ItemType == "DoubleRare").Count, itemStatsList.First(i => i.ItemType == "DoubleRare").AveragePrice,
                itemStatsList.First(i => i.ItemType == "UltraRare").Count, itemStatsList.First(i => i.ItemType == "UltraRare").AveragePrice,
                itemStatsList.First(i => i.ItemType == "IllustrationRare").Count, itemStatsList.First(i => i.ItemType == "IllustrationRare").AveragePrice,
                itemStatsList.First(i => i.ItemType == "HoloRare").Count, itemStatsList.First(i => i.ItemType == "HoloRare").AveragePrice,
                run
            );

            var itemStatsLog = string.Join(", ", itemStatsList.Select(i => $"{i.ItemType}: {i.Count}"));
            // Output log in the format you specified
            var doubleRareCount = itemStatsList.First(i => i.ItemType == "DoubleRare").Count;
            var ultraRareCount = itemStatsList.First(i => i.ItemType == "UltraRare").Count;
            var illustrationRareCount = itemStatsList.First(i => i.ItemType == "IllustrationRare").Count;
            var holoRareCount = itemStatsList.First(i => i.ItemType == "HoloRare").Count;

            Logger.OutputOk($"DoubleRare: {doubleRareCount}, UltraRare: {ultraRareCount}, IllustrationRare: {illustrationRareCount}, HoloRare: {holoRareCount}", run.RunId);
            await page.CloseAsync();

            return itemStatsList;
        }

        static async Task GetSellerInfoAsync(string userName, IBrowserContext context, Run run)
        {
            var page = await context.NewPageAsync();
            try
            {
                page.SetDefaultTimeout(5000);
                string userDetailsPage = $"https://www.cardmarket.com/it/Pokemon/Users/{userName}";
                var response = await page.GotoAsync(userDetailsPage, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

                if (response == null)
                {
                    Logger.OutputError($"No response received for {userName}.\n", runID);

                    return;
                }

                if (response.Status == 429)
                {
                    Logger.OutputWarning($"HTTP 429 received. Waiting 60 seconds before retrying...", runID);
                    await page.CloseAsync();
                    await Task.Delay(60000); // Wait 1 minute
                    await GetSellerInfoAsync(userName, context, run); // Retry
                    return;
                }

                if (!response.Ok)
                {
                    Logger.OutputError($"HTTP {response.Status} for {userName}: {response.StatusText}\n", runID);

                    return;
                }

                // Helper to safely extract text
                async Task<string> SafeGetInnerTextAsync(string selector)
                {
                    var element = await page.QuerySelectorAsync(selector);
                    return element != null ? await element.InnerTextAsync() : "";
                }

                var seller = new Sellers
                {
                    Username = await SafeGetInnerTextAsync("#PublicProfileHeadline"),
                    Type = await SafeGetInnerTextAsync("div.d-flex.align-items-center span:nth-of-type(2)"),
                    Info = await SafeGetInnerTextAsync("div.d-flex.align-items-center span.personalInfo-light"),
                    CardMarketRank = await SafeGetInnerTextAsync("div.col.col-md-auto span.personalInfo")
                };

                // Country selectors
                var countryComplexSelector = "div.col-12.col-md-6 div.d-flex.align-items-center.justify-content-start.flex-wrap.personalInfo p.mb-1.w-100";
                var countrySimpleSelector = "div#PersonalInfoRow p.mb-1.w-100";

                var countryElements = await page.QuerySelectorAllAsync(countryComplexSelector);
                if (countryElements.Count > 0)
                {
                    var countryParts = new List<string>();
                    foreach (var elem in countryElements)
                        countryParts.Add(await elem.InnerTextAsync());
                    seller.Country = string.Join("|", countryParts);
                }
                else
                {
                    var simpleCountryElements = await page.QuerySelectorAllAsync(countrySimpleSelector);
                    seller.Country = simpleCountryElements.Count > 0
                        ? await simpleCountryElements.First().InnerTextAsync()
                        : "Unknown";
                }

                // Stats
                var stats = await page.QuerySelectorAllAsync("#collapsibleAdditionalInfo dl dt");
                var statsDict = new Dictionary<string, int>();
                foreach (var dt in stats)
                {
                    string key = (await dt.InnerTextAsync()).Trim();
                    var dd = await dt.EvaluateHandleAsync("el => el.nextElementSibling");
                    if (dd != null)
                    {
                        string valText = (await dd.AsElement().InnerTextAsync()).Trim();
                        if (int.TryParse(valText, out int val))
                            statsDict[key] = val;
                    }
                }

                seller.Buy = statsDict.GetValueOrDefault("Acquisti");
                seller.Sell = statsDict.GetValueOrDefault("Vendite");
                seller.BuyNotPayed = statsDict.GetValueOrDefault("Ordini non pagati");
                seller.SellNotSent = statsDict.GetValueOrDefault("Non spediti");
                seller.BuyNotReceived = statsDict.GetValueOrDefault("Acquisti non ricevuti");
                seller.SellNotArrived = statsDict.GetValueOrDefault("Spedizioni non arrivate");

                // Singles
                var singlesSelector = "a[href*='/Offers/Singles'] span.bracketed";
                var singlesElement = await page.QuerySelectorAsync(singlesSelector);
                if (singlesElement != null)
                {
                    var singlesText = await singlesElement.InnerTextAsync();
                    if (int.TryParse(singlesText, out int singlesCount))
                        seller.Singles = singlesCount;
                }
                else
                {
                    seller.Singles = 0;
                }

                var dataAccess = new dal();
                await dataAccess.InsertSellerAsync(seller, run);

                Logger.OutputOk($"Info collected", runID);

            }
            catch (Exception ex)
            {
                Logger.OutputError($"Failed to collect info for {userName}: {ex.Message}", runID);

            }
            finally
            {
                await page.CloseAsync();
            }
        }

        static async Task<(int totalCount, float avgPrice)> GetTotalCountFromUserPage(IPage page, string url, string userName)
        {
            try
            {
                var response = await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.Load,
                    Timeout = 5000
                });

                if (response?.Status == 429 || (await page.ContentAsync()).Contains("HTTP ERROR 429"))
                {
                    Logger.OutputWarning($"Received HTTP 429. Waiting 60 seconds and retry...", runID);
                    await Task.Delay(TimeSpan.FromSeconds(60));
                    return await GetTotalCountFromUserPage(page, url, userName);
                }

                var noResultsLocator = page.Locator("p.noResults");
                if (await noResultsLocator.IsVisibleAsync())
                {
                    return (0, 0f);
                }

                var articleRows = await page.Locator("div[id^='articleRow']").AllAsync();
                int totalCount = 0;
                float totalPriceSum = 0;
                int priceCount = 0;

                foreach (var row in articleRows)
                {
                    try
                    {
                        var priceLocator = row.Locator("span.color-primary.fw-bold").First;
                        var priceText = (await priceLocator.InnerTextAsync())
                                             .Trim()
                                             .Replace("€", "")
                                             .Replace(",", ".");

                        float price = float.TryParse(
                            priceText,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var p
                        ) ? p : 0;

                        totalPriceSum += price;
                        priceCount++;
                    }
                    catch (Exception ex)
                    {
                        Logger.OutputWarning($"Skipping one offer row due to error: {ex.Message}", runID);
                    }
                }

                float totalPriceAvg = priceCount > 0 ? totalPriceSum / priceCount : 0;

                string totalCountText = await page.Locator("span.total-count").Nth(0).InnerTextAsync();
                if (int.TryParse(totalCountText, out int totalCountCalc))
                {
                    totalCount = totalCountCalc;
                }
                else
                {
                    totalCount = 0;
                }

                return (totalCount, totalPriceAvg);
            }
            catch (TimeoutException ex)
            {
                Logger.OutputError($"Timeout navigating to URL for {userName} exception: {ex.Message}", runID);
                return (0, 0f);
            }
        }

        static async Task<List<CardsInfo>> LoadMoreAndGetCardInfoAsync(Card card, IBrowserContext context)
        {
            var page = await context.NewPageAsync();

            // Navigate to the card URL
            await page.GotoAsync(card.CardUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

            // Initialize a list to store CardInfo objects
            var cardInfoList = new List<CardsInfo>();

            for (int i = 0; i < 10; i++)
            {
                var loadMoreButton = await page.QuerySelectorAsync("#loadMoreButton");

                if (loadMoreButton == null)
                {
                    break;
                }

                try
                {
                    await page.WaitForSelectorAsync("#loadMoreButton:visible", new PageWaitForSelectorOptions { Timeout = 5000 });
                    await loadMoreButton.ClickAsync();
                    await page.WaitForTimeoutAsync(1000);
                }
                catch (TimeoutException)
                {
                    break;
                }
            }


            var articleRows = await page.QuerySelectorAllAsync(".article-row");

            foreach (var row in articleRows)
            {
                var sellerUsernameElement = await row.QuerySelectorAsync(".seller-name a");
                var sellerUsername = sellerUsernameElement is not null
                    ? await sellerUsernameElement.InnerTextAsync()
                    : string.Empty;

                var sellerCountryElement = await row.QuerySelectorAsync(".icon[aria-label]");
                var sellerCountry = sellerCountryElement is not null
                    ? await sellerCountryElement.GetAttributeAsync("aria-label")
                    : string.Empty;

                var cardConditionElement = await row.QuerySelectorAsync(".article-condition .badge");
                var cardCondition = cardConditionElement is not null
                    ? await cardConditionElement.InnerTextAsync()
                    : string.Empty;

                var cardLanguageElement = await row.QuerySelectorAsync(".product-attributes .icon[aria-label]");
                var cardLanguage = cardLanguageElement is not null
                    ? await cardLanguageElement.GetAttributeAsync("aria-label")
                    : string.Empty;

                var cardPriceElement = await row.QuerySelectorAsync(".color-primary");
                string cardPriceText = cardPriceElement is not null
                    ? await cardPriceElement.InnerTextAsync()
                    : string.Empty;

                decimal cardPrice = 0;
                if (!string.IsNullOrEmpty(cardPriceText))
                {
                    cardPriceText = cardPriceText.Replace("€", "").Trim();

                    // Remove thousands separator and replace decimal comma with dot
                    cardPriceText = cardPriceText.Replace(".", "").Replace(",", ".");

                    decimal.TryParse(cardPriceText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out cardPrice);
                }

                var cardQuantityElement = await row.QuerySelectorAsync(".item-count");
                string cardQuantityText = cardQuantityElement is not null
                    ? await cardQuantityElement.InnerTextAsync()
                    : string.Empty;

                int cardQuantity = int.TryParse(cardQuantityText?.Trim(), out var quantity) ? quantity : 0;

                var cardCommentElement = await row.QuerySelectorAsync(".product-comments .d-block.text-truncate");
                var cardComment = cardCommentElement is not null
                    ? await cardCommentElement.InnerTextAsync()
                    : string.Empty;

                var cardInfo = new CardsInfo(
                    card.CardID,
                    sellerUsername,
                    sellerCountry,
                    cardCondition,
                    cardLanguage,
                    cardComment,
                    cardPrice,
                    cardQuantity
                );


                cardInfoList.Add(cardInfo);
            }

            // Close the page after scraping
            await page.CloseAsync();

            // Return the list of card info
            return cardInfoList;
        }

    }
}