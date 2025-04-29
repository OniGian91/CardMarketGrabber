using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using CardGrabber.DAL;
using Azure;

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
                await RunScraperOperation();

                // Wait for the next interval (1 minute)
                Console.WriteLine($"Waiting for {intervalInMinutes} minute(s)...\n");
                await Task.Delay(intervalInMilliseconds);
            }
        }

        static async Task RunScraperOperation()
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
            IEnumerable<Users> users = await dataAccess.GetDataAsync();

            // Iterate over each user
            foreach (var user in users)
            {
                Console.WriteLine($"Processing user: {user.Name}");

                // Construct the URL dynamically for each user
                string url = $"https://www.cardmarket.com/it/Pokemon/Users/{user.Name}/Offers/Singles?maxPrice=1&condition=3&idRarity=199";

                try
                {
                    // Go to the page with a longer timeout and change the wait condition
                    var response = await page.GotoAsync(url,
                        new PageGotoOptions
                        {
                            WaitUntil = WaitUntilState.Load,  // Wait for full page load
                            Timeout = 60000 // Increase timeout to 60 seconds
                        });
                    if (response?.Status == 429 || (await page.ContentAsync()).Contains("HTTP ERROR 429"))
                    {
                        Console.WriteLine($"Received HTTP 429 for user {user.Name}. Waiting 1 minute...");
                        await Task.Delay(TimeSpan.FromMinutes(1));
                        continue;
                    }
                }

                catch (TimeoutException e)
                {
                    Console.WriteLine($"Navigation timeout for user {user.Name}: {e.Message}");
                    continue; // Move on to the next user
                }

                var noResultsLocator = page.Locator("p.noResults");

                bool noResults = false;
                var totalCountText = "0";
                if (await noResultsLocator.IsVisibleAsync())
                {
                    Console.WriteLine($"No results found for user {user.Name}. Skipping.");
                    noResults = true;
                }
                if (!noResults)
                {
                    // Wait for the span to be visible
                    try
                    {

                    
                    await page.Locator("span.total-count").Nth(0).WaitForAsync(new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = 5000  
                    });
                    // Get the content from the first span.total-count element
                    totalCountText = await page.Locator("span.total-count").Nth(0).InnerTextAsync();
                    }
                    catch
                    {
                        Console.WriteLine($"Error gettint {user.Name}");
                        totalCountText = "0";
                    }
                }
               

                if (int.TryParse(totalCountText, out int totalCount))
                {
                    Console.WriteLine($"Total Count for user {user.Name}: {totalCount}");

                    // Pass the total count and user Id to WriteData
                    dataAccess.WriteData(user.userId, totalCount);
                }
                else
                {
                    Console.WriteLine($"Failed to parse total count for user {user.Name}: {totalCountText}");
                }
                // ✅ Wait 10 seconds before the next user

                Console.WriteLine("Waiting 10 seconds before next user...\n");
                await Task.Delay(10000);
            }

            Console.WriteLine("Scraper operation complete.");

            await browser.CloseAsync();
        }
    }
}
