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
                usernames = await getUsers();
                // Remove duplicates based on the 'Name' property using LINQ
                usernames = usernames
                    .GroupBy(user => user.Name)  // Group by the 'Name' property
                    .Select(group => group.First())  // Select the first user from each group
                    .ToList();  // Convert the result back to a list
                Console.WriteLine($"Get {usernames.Count()} users...\n");
                await getItemsNum(usernames);
                // Wait for the next interval (1 minute)
                Console.WriteLine($"Waiting for {intervalInMinutes} minute(s)...\n");
                await Task.Delay(intervalInMilliseconds);
            }
        }

        static async Task<List<Users>> getUsers()
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
            string listingUrl = "https://www.cardmarket.com/it/Pokemon/Products/Singles?idCategory=51&idExpansion=0&idRarity=199&sortBy=price_asc&perSite=20";
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
                    await page.GotoAsync(productUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

                    // Wait for sellers to load
                    await page.WaitForSelectorAsync("span.seller-name");

                    // Collect usernames
                    var sellerSpans = await page.Locator("span.seller-name").AllAsync();
                    foreach (var span in sellerSpans)
                    {
                        var anchorLocator = span.Locator("a").First;
                        if (await anchorLocator.CountAsync() > 0)
                        {
                            var username = await anchorLocator.InnerTextAsync();
                            Console.WriteLine($"Seller: {username}");

                            // Create a new Users object and add it to the list
                            var user = new Users
                            {
                                Name = username
                            };
                            usersList.Add(user);
                        }
                    }

                    // Go back to the product listing page
                    await page.GoBackAsync(new PageGoBackOptions { WaitUntil = WaitUntilState.Load });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing product at index {i}: {ex.Message}");
                }

                // Wait a short time between iterations to avoid rate limiting
                await Task.Delay(1000);
            }

            Console.WriteLine("RunScraperOperationV2 complete.");
            await browser.CloseAsync();

            // Return the list of Users
            return usersList;
        }




        static async Task getItemsNum(List<Users> users)
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
                Console.WriteLine($"Processing user: {user.Name}");

                string urlDoubleRare = $"https://www.cardmarket.com/it/Pokemon/Users/{user.Name}/Offers/Singles?maxPrice=1&condition=3&idRarity=199";
                string urlUltraRare = $"https://www.cardmarket.com/it/Pokemon/Users/{user.Name}/Offers/Singles?maxPrice=1&condition=3&idRarity=54";

                int doubleRareCount = await GetTotalCountFromUserPage(page, urlDoubleRare, user.Name);
                int ultraRareCount = await GetTotalCountFromUserPage(page, urlUltraRare, user.Name);

                Console.WriteLine($"[RESULT] {user.Name} - DoubleRare: {doubleRareCount}, UltraRare: {ultraRareCount}");

                dataAccess.WriteData(user.Name, doubleRareCount, ultraRareCount);

                Console.WriteLine("Waiting 10 seconds before next user...\n");
                await Task.Delay(10000);
            }

            Console.WriteLine("Scraper operation complete.");

            await browser.CloseAsync();
        }



        static async Task<int> GetTotalCountFromUserPage(IPage page, string url, string userName)
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
                    Console.WriteLine($"Received HTTP 429 for user {userName}. Waiting 1 minute...");
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    return 0;
                }

                var noResultsLocator = page.Locator("p.noResults");
                if (await noResultsLocator.IsVisibleAsync())
                {
                    Console.WriteLine($"No results found for {userName} on URL: {url}");
                    return 0;
                }

                try
                {
                    await page.Locator("span.total-count").Nth(0).WaitForAsync(new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = 5000
                    });

                    string totalCountText = await page.Locator("span.total-count").Nth(0).InnerTextAsync();
                    if (int.TryParse(totalCountText, out int totalCount))
                    {
                        return totalCount;
                    }
                    else
                    {
                        Console.WriteLine($"Failed to parse total count for {userName}: {totalCountText}");
                        return 0;
                    }
                }
                catch
                {
                    Console.WriteLine($"Error getting count for {userName} on URL: {url}");
                    return 0;
                }
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"Timeout navigating to URL for {userName}: {ex.Message}");
                return 0;
            }
        }


    }
}