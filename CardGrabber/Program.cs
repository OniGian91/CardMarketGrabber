using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using CardGrabber.DAL;
using Azure;
using System.Linq;

namespace CardMarketScraper
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Set the interval for running the operation (in milliseconds)
            int intervalInMinutes = 1; // Set to 1 minute for periodic execution
            int intervalInMilliseconds = intervalInMinutes * 60 * 1000;

            // Run the operation indefinitely
            while (true)
            {

                List<Users> usernames = new List<Users>();
                Guid newGuid = Guid.NewGuid();
                Console.WriteLine($"[START] Starting CardMarketGrabber. runIdentifier {newGuid.ToString()}\n");

                string listingUrlDoubleRare = "https://www.cardmarket.com/it/Pokemon/Products/Singles?idCategory=51&idExpansion=0&idRarity=199&sortBy=price_asc&perSite=20";
                string listingUrlIllustrationRare = "https://www.cardmarket.com/it/Pokemon/Products/Singles?idCategory=51&idExpansion=0&idRarity=280&sortBy=price_asc&perSite=20";
                string listingUrlUltraRare = "https://www.cardmarket.com/it/Pokemon/Products/Singles?idCategory=51&idExpansion=0&idRarity=54&sortBy=price_asc&perSite=20";

                usernames.AddRange(await getUsersFromUrl(listingUrlDoubleRare,"DoubleRare"));
                //usernames.AddRange(await getUsersFromUrl(listingUrlIllustrationRare, "IllustrationRare"));
                //usernames.AddRange(await getUsersFromUrl(listingUrlUltraRare, "UltraRare"));


                // Remove duplicates based on the 'Name' property using LINQ
                usernames = usernames
                    .GroupBy(user => user.Name)  // Group by the 'Name' property
                    .Select(group => group.First())  // Select the first user from each group
                    .ToList();  // Convert the result back to a list
                Console.WriteLine($"[INFO] GetUsers() completed, loaded {usernames.Count()} univocal users...\n");
                Console.WriteLine($"[INFO] Starting to GetItemsInfo...\n");
                await getItemsNum(usernames, newGuid);
                // Wait for the next interval (1 minute)
                Console.WriteLine($"Waiting for {intervalInMinutes} minute(s)...\n");
                await Task.Delay(intervalInMilliseconds);
            }
        }

        static async Task<List<Users>> getUsersFromUrl(string listingUrl, string type)
        {
            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true, // Set to false for debugging
                SlowMo = 100
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
            });

            var page = await context.NewPageAsync();

            // Users a list to store usernames
            List<Users> usersList = new List<Users>();

            // 1. Navigate to main product page
            await page.GotoAsync(listingUrl);

            // 2. Get all product row divs
            var productDivs = await page.Locator("div[id^='productRow']").AllAsync();

            for (int i = 0; i < productDivs.Count; i++)
            {
                var product = productDivs[i];

                try
                {
                    // Find the <a> inside the product div
                    var productHandle = await product.ElementHandleAsync();
                    if (productHandle == null) continue;

                    // Find all <a> elements inside the product
                    var linkHandles = await productHandle.QuerySelectorAllAsync("a");

                    string productUrl = null;

                    foreach (var link in linkHandles)
                    {
                        var href = await link.GetAttributeAsync("href");
                        if (!string.IsNullOrEmpty(href) && href.Contains("/Singles/"))
                        {
                            productUrl = $"https://www.cardmarket.com{href}?sellerCountry=17&minCondition=3";
                            break; // stop at the first matching link
                        }
                    }

                    if (string.IsNullOrEmpty(productUrl)) continue;

                    // Navigate to the product detail page
                    // Navigate to the product detail page with 429 handling
                    var response = await page.GotoAsync(productUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

                    if (response?.Status == 429 || (await page.ContentAsync()).Contains("HTTP ERROR 429"))
                    {
                        Console.WriteLine($"[ERROR] {type} HTTP 429 Too Many Requests for product at index {i}. Waiting 1 minute and retrying...");
                        await Task.Delay(TimeSpan.FromMinutes(1));

                        // Retry navigation once after delay
                        response = await page.GotoAsync(productUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

                        if (response?.Status == 429 || (await page.ContentAsync()).Contains("HTTP ERROR 429"))
                        {
                            Console.WriteLine($"[ERROR] {type} Still receiving 429 after retry. Skipping this product.");
                            continue;
                        }
                    }
                    // Check if the product page has no sellers
                    var noResultsLocator = page.Locator("p.noResults");
                    if (await noResultsLocator.IsVisibleAsync())
                    {
                        Console.WriteLine($"[INFO] ({type}) No sellers found for product at index {i}. Skipping...");
                        await page.GoBackAsync(new PageGoBackOptions { WaitUntil = WaitUntilState.Load });
                        continue;
                    }

                    // Wait for sellers to load
                    await page.WaitForSelectorAsync("span.seller-name");

                    // Collect usernames
                    var sellerSpans = await page.Locator("span.seller-name").AllAsync();
                    var newUsers = 0;
                    foreach (var span in sellerSpans)
                    {

                        var anchorLocator = span.Locator("a").First;
                        if (await anchorLocator.CountAsync() > 0)
                        {
                            var username = await anchorLocator.InnerTextAsync();
                            //Console.WriteLine($"Seller: {username}");

                            var user = new Users
                            {
                                Name = username
                            };
                            usersList.Add(user);
                            newUsers++;
                        }
                    }
                    Console.WriteLine($"[INFO] ({type}) Added: {newUsers} sellers to the list");

                    // Go back to the product listing page
                    await page.GoBackAsync(new PageGoBackOptions { WaitUntil = WaitUntilState.Load });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] {type} Error processing product at index {i}: {ex.Message}");
                }

                // Wait a short time between iterations to avoid rate limiting
                await Task.Delay(500);
            }

            await browser.CloseAsync();

            // Return the list of Users
            return usersList;
        }


        static async Task getItemsNum(List<Users> users, Guid runIdentifier)
        {
            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true, // See what's happening
                SlowMo = 100 // Slow down to watch if needed
            });

            // Create a new context with a desktop viewport
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
            });

            var page = await context.NewPageAsync();
            var dataAccess = new dal(); // Ensure you pass the connection string here

            // Get the list of users from the database

            // Iterate over each user
            foreach (var user in users)
            {
                Console.WriteLine($"[STARTING] {user.Name}");

                string urlDoubleRare = $"https://www.cardmarket.com/it/Pokemon/Users/{user.Name}/Offers/Singles?maxPrice=0.3&condition=3&idRarity=199";
                string urlUltraRare = $"https://www.cardmarket.com/it/Pokemon/Users/{user.Name}/Offers/Singles?maxPrice=1&condition=3&idRarity=54";
                string urlIllustrationRare = $"https://www.cardmarket.com/it/Pokemon/Users/{user.Name}/Offers/Singles?maxPrice=0.8&condition=3&idRarity=280";

                var (doubleRareCount, doubleRareAvgPrice) = await GetTotalCountFromUserPage(page, urlDoubleRare, user.Name);
                var (ultraRareCount, ultraRareAvgPrice) = await GetTotalCountFromUserPage(page, urlUltraRare, user.Name);
                var (illustrationRareCount, illustrationRareAvgPrice) = await GetTotalCountFromUserPage(page, urlIllustrationRare, user.Name);


                Console.WriteLine($"[RESULT] DoubleRare: {doubleRareCount}, UltraRare: {ultraRareCount}, IllustrationRare: {illustrationRareCount}");

                dataAccess.WriteData(user.Name, doubleRareCount, doubleRareAvgPrice, ultraRareCount, ultraRareAvgPrice, illustrationRareCount, illustrationRareAvgPrice, runIdentifier);

                Console.WriteLine("[INFO] Waiting 10 seconds before next user...\n");
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
                    Timeout = 60000
                });

                if (response?.Status == 429 || (await page.ContentAsync()).Contains("HTTP ERROR 429"))
                {
                    Console.WriteLine($"[ERROR] Error processing {userName} - Received HTTP 429. Waiting 1 minute and retry...");
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    return await GetTotalCountFromUserPage(page, url, userName); // RECURSIVE RETRY
                }

                var noResultsLocator = page.Locator("p.noResults");
                if (await noResultsLocator.IsVisibleAsync())
                {
                    return (0, 0f);
                }

                // Find all article rows
                var articleRows = await page.Locator("div[id^='articleRow']").AllAsync();
                int totalCount = 0;
                float totalPriceSum = 0;

                foreach (var row in articleRows)
                {
                    try
                    {
                        var priceSpan = row.Locator("span.color-primary.fw-bold");
                        string priceText = await priceSpan.InnerTextAsync();
                        priceText = priceText?.Trim().Replace("€", "").Replace(",", ".");

                        float price = float.TryParse(priceText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float p) ? p : 0;

                        var quantitySpan = row.Locator("span.item-count");
                        string quantityText = await quantitySpan.InnerTextAsync();
                        int quantity = int.TryParse(quantityText?.Trim(), out int q) ? q : 1;

                        totalCount += quantity;
                        totalPriceSum += (price * quantity);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WARN] Skipping one offer row due to error: {ex.Message}");
                        continue;
                    }
                }

                float avgPrice = totalCount > 0 ? totalPriceSum / totalCount : 0f;

                return (totalCount, avgPrice);
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"[ERROR] Timeout navigating to URL for {userName} exception: {ex.Message}");
                return (0, 0f);
            }
        }



    }
}