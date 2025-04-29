using Microsoft.Playwright;
using CardGrabber.DAL;

namespace CardMarketScraper
{
    class Program
    {
        #region Configurations
        public static bool debugMode = false;
        public static bool useDBUsers = false;
        public static string scraperUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";
        #endregion


        static async Task Main(string[] args)
        {
            int intervalInMinutes = 1;
            int intervalInMilliseconds = intervalInMinutes * 60 * 1000;

            while (true)
            {
                Guid runId = Guid.NewGuid();
                Console.WriteLine($"[START] Starting CardMarketGrabber. runIdentifier {runId}\n");

                var dataAccess = new dal();
                IEnumerable<Users> usersFromDb = await dataAccess.GetUsers();
                List<Users> finalUsers;
                finalUsers = new List<Users>();

                if (debugMode)
                {
                    finalUsers.Add(new Users { Name = "CheckpointGallarate" });
                }
                else
                {

                    if (usersFromDb == null || !usersFromDb.Any() || useDBUsers == false)
                    {
                        if (!useDBUsers)
                        {
                            Console.WriteLine($"[INFO] There are some users on DB: {usersFromDb.Count()}. Fetching from listing URLs anyway due config...");
                        }
                        else
                        {
                            Console.WriteLine("[INFO] No users in DB. Fetching from listing URLs...");
                        }
                        

                        string listingUrlDoubleRare = "https://www.cardmarket.com/it/Pokemon/Products/Singles?idCategory=51&idExpansion=0&idRarity=199&sortBy=price_asc&perSite=20&site=4";
                        string listingUrlIllustrationRare = "https://www.cardmarket.com/it/Pokemon/Products/Singles?idCategory=51&idExpansion=0&idRarity=280&sortBy=price_asc&perSite=20&site=4";
                        string listingUrlUltraRare = "https://www.cardmarket.com/it/Pokemon/Products/Singles?idCategory=51&idExpansion=0&idRarity=54&sortBy=price_asc&perSite=20&site=4";

                        finalUsers.AddRange(await getUsersFromUrl(listingUrlDoubleRare, "DoubleRare", 0.30m));
                        finalUsers.AddRange(await getUsersFromUrl(listingUrlIllustrationRare, "IllustrationRare", 1.00m));
                        finalUsers.AddRange(await getUsersFromUrl(listingUrlUltraRare, "UltraRare", 0.70m));
                        //finalUsers.AddRange(usersFromDb);
                        finalUsers = finalUsers
                            .GroupBy(u => u.Name)
                            .Select(g => g.First())
                            .ToList();

                        foreach (var user in finalUsers)
                        {
                            dataAccess.InsertUser(user);
                        }
                    }
                    else
                    {
                        Console.WriteLine("[INFO] Loaded users from DB.");
                        finalUsers = usersFromDb.ToList();
                    }
                }

                Console.WriteLine($"[INFO] Loaded {finalUsers.Count} unique users.\n");
                Console.WriteLine("[INFO] Starting to GetItemsInfo...\n");

                await getItemsNum(finalUsers, runId);

                Console.WriteLine($"Waiting for {intervalInMinutes} minute(s)...\n");
                await Task.Delay(intervalInMilliseconds);
            }
        }

        static async Task<List<Users>> getUsersFromUrl(string listingUrl, string type, decimal priceThreshold)
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

            var page = await context.NewPageAsync();

            List<Users> usersList = new List<Users>();

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
                        Console.WriteLine($"[ERROR] {type} HTTP 429 Too Many Requests for product at index {i}. Waiting 30 sec and retrying...");
                        await Task.Delay(TimeSpan.FromSeconds(30));

                        response = await page.GotoAsync(productUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

                        if (response?.Status == 429 || (await page.ContentAsync()).Contains("HTTP ERROR 429"))
                        {
                            Console.WriteLine($"[ERROR] {type} Still receiving 429 after retry. Skipping this product.");
                            continue;
                        }
                    }
                    var noResultsLocator = page.Locator("p.noResults");
                    if (await noResultsLocator.IsVisibleAsync())
                    {
                        Console.WriteLine($"[INFO] ({type}) No sellers found for product at index {i}. Skipping...");
                        await page.GoBackAsync(new PageGoBackOptions { WaitUntil = WaitUntilState.Load });
                        continue;
                    }


                    // Wait for all article rows to be loaded
                    await page.WaitForSelectorAsync("div.article-row");

                    // Select all article rows
                    var articleRows = await page.Locator("div.article-row").AllAsync();

                    var newUsers = 0;

                    foreach (var row in articleRows)
                    {
                        var priceLocator = row.Locator("div.price-container span.fw-bold");
                        var priceCount = await priceLocator.CountAsync();

                        if (priceCount > 0)
                        {
                            var priceText = await priceLocator.First.InnerTextAsync();

                            // Clean and parse price: replace comma with dot, remove euro sign
                            var cleanedPrice = priceText.Replace("€", "").Trim().Replace(",", ".");
                            if (decimal.TryParse(cleanedPrice, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal price))
                            {
                                if (price <= priceThreshold)
                                {
                                    var sellerAnchor = row.Locator("span.seller-name a");
                                    if (await sellerAnchor.CountAsync() > 0)
                                    {
                                        var username = await sellerAnchor.First.InnerTextAsync();
                                        var user = new Users { Name = username };
                                        usersList.Add(user);
                                        newUsers++;
                                    }
                                }
                            }
                        }
                    }

                    Console.WriteLine($"[INFO] ({type}) Added: {newUsers} sellers to the list");

                    await page.GoBackAsync(new PageGoBackOptions { WaitUntil = WaitUntilState.Load });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] {type} Error processing product at index {i}: {ex.Message}");
                }

                await Task.Delay(500);
            }

            await browser.CloseAsync();

            return usersList;
        }


        static async Task getItemsNum(List<Users> users, Guid runIdentifier)
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

            var page = await context.NewPageAsync();
            var dataAccess = new dal();


            for (int i = 0; i < users.Count; i++)
            {
                var user = users[i];
                Console.WriteLine($"[STARTING] {user.Name} ({i + 1}/{users.Count})");

                // idLanguage=5 per filtrare solo roba in italiano
                string urlDoubleRare = $"https://www.cardmarket.com/it/Pokemon/Users/{user.Name}/Offers/Singles?maxPrice=0.3&condition=3&idRarity=199&sortBy=price_asc";
                string urlUltraRare = $"https://www.cardmarket.com/it/Pokemon/Users/{user.Name}/Offers/Singles?maxPrice=1&condition=3&idRarity=54&sortBy=price_asc";
                string urlIllustrationRare = $"https://www.cardmarket.com/it/Pokemon/Users/{user.Name}/Offers/Singles?maxPrice=0.8&condition=3&idRarity=280&sortBy=price_asc";
                var (doubleRareCount, doubleRareAvgPrice) = await GetTotalCountFromUserPage(page, urlDoubleRare, user.Name);
                var (ultraRareCount, ultraRareAvgPrice) = await GetTotalCountFromUserPage(page, urlUltraRare, user.Name);
                var (illustrationRareCount, illustrationRareAvgPrice) = await GetTotalCountFromUserPage(page, urlIllustrationRare, user.Name);

                dataAccess.WriteData(user.Name, doubleRareCount, doubleRareAvgPrice, ultraRareCount, ultraRareAvgPrice, illustrationRareCount, illustrationRareAvgPrice, runIdentifier);

                Console.WriteLine($"[RESULT] DoubleRare: {doubleRareCount}, UltraRare: {ultraRareCount}, IllustrationRare: {illustrationRareCount}");
                Console.WriteLine($"[INFO] Waiting 10 seconds before next user...");

                await Task.Delay(10000);
            }


            Console.WriteLine("Scraper operation complete.");

            await browser.CloseAsync();
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
                    Console.WriteLine($"[ERROR] Error processing {userName} - Received HTTP 429. Waiting 1 minute and retry...");
                    await Task.Delay(TimeSpan.FromMinutes(1));
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
                        Console.WriteLine($"[WARN] Skipping one offer row due to error: {ex.Message}");
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
                Console.WriteLine($"[ERROR] Timeout navigating to URL for {userName} exception: {ex.Message}");
                return (0, 0f);
            }
        }
    }
}