using Azure;
using CardGrabber.Configuration;
using CardGrabber.DAL;
using CardGrabber.Models;
using Microsoft.Playwright;
using System.Text.Json;



namespace CardGrabber.Services.Playwright
{
    internal class PlaywrightManager
    {
        private readonly AppSettings _config;

        public PlaywrightManager(AppSettings config)
        {
            _config =  config;
        }
        public async Task<List<Sellers>> GetSellersFromDB(IBrowserContext context,  Run run)
        {
            var dataAccess = new dal();

            Logger.OutputInfo($"Starting to get sellers from DataBase", run.RunId);
            List<Sellers> dbSellers = await dataAccess.GetSellers();
            Logger.OutputOk($"Get {dbSellers.Count()} sellers from DataBase\n", run.RunId);
            return dbSellers;
        }

        public async Task<List<Sellers>> CollectSellersFromSite(IBrowserContext context, Run run)
        {
            List<Sellers> siteSellers = new List<Sellers>();

            Logger.OutputInfo("Starting to get sellers from site", run.RunId);

            var page = await context.NewPageAsync();
            page.SetDefaultTimeout(5000);
            await page.GotoAsync("https://www.cardmarket.com/it/Pokemon/Products/Singles");

            var productDivs = await page.Locator("div[id^='productRow']").AllAsync();

            // Step 1: Collect all product URLs first
            List<string> productUrls = new List<string>();

            foreach (var product in productDivs)
            {
                try
                {
                    var productHandle = await product.ElementHandleAsync();
                    if (productHandle == null) continue;

                    var linkHandles = await productHandle.QuerySelectorAllAsync("a");
                    foreach (var link in linkHandles)
                    {
                        var href = await link.GetAttributeAsync("href");
                        if (!string.IsNullOrEmpty(href) && href.Contains("/Singles/"))
                        {
                            string productUrl = $"https://www.cardmarket.com{href}?sellerCountry=17&minCondition=3";
                            productUrls.Add(productUrl);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.OutputError($"Error extracting product URL: {ex.Message}", run.RunId);
                }
            }

            // Step 2: Visit each product page independently
            for (int i = 0; i < productUrls.Count; i++)
            {
                string productUrl = productUrls[i];
                try
                {
                    var response = await page.GotoAsync(productUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

                    if (response?.Status == 429 || (await page.ContentAsync()).Contains("HTTP ERROR 429"))
                    {
                        Logger.OutputWarning($"HTTP 429 Too Many Requests at index {i}. Waiting 30 sec and retrying...", run.RunId);
                        await Task.Delay(TimeSpan.FromSeconds(30));

                        response = await page.GotoAsync(productUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

                        if (response?.Status == 429 || (await page.ContentAsync()).Contains("HTTP ERROR 429"))
                        {
                            Logger.OutputError($"Still receiving 429 after retry. Skipping this product.", run.RunId);
                            continue;
                        }
                    }

                    var noResultsLocator = page.Locator("p.noResults");
                    if (await noResultsLocator.IsVisibleAsync())
                    {
                        Logger.OutputDebug($"No sellers found for product at index {i}. Skipping...", run.RunId);
                        continue;
                    }

                    await page.WaitForSelectorAsync("div.article-row");
                    var articleRows = await page.Locator("div.article-row").AllAsync();

                    int newUsers = 0;
                    foreach (var row in articleRows)
                    {
                        var priceLocator = row.Locator("div.price-container span.fw-bold");
                        if (await priceLocator.CountAsync() > 0)
                        {
                            var sellerAnchor = row.Locator("span.seller-name a");
                            if (await sellerAnchor.CountAsync() > 0)
                            {
                                var username = await sellerAnchor.First.InnerTextAsync();
                                siteSellers.Add(new Sellers { Username = username });
                                newUsers++;
                            }
                        }
                    }

                    Logger.OutputOk($"Added: {newUsers} sellers to the list...", run.RunId);
                }
                catch (Exception ex)
                {
                    Logger.OutputError($"Error processing product at index {i}: {ex.Message}", run.RunId);
                }

                //await Task.Delay(1000);
            }

            // Step 3: Deduplicate
            siteSellers = siteSellers
                .GroupBy(u => u.Username)
                .Select(g => g.First())
                .ToList();

            Logger.OutputOk($"Get {siteSellers.Count} sellers from site\n", run.RunId);
            return siteSellers;
        }

        static async Task<(int totalCount, float avgPrice)> GetTotalCountFromUserPage(IPage page, string url, string userName, Run run)
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
                    Logger.OutputWarning($"Received HTTP 429. Waiting 60 seconds and retry...", run.RunId);
                    await Task.Delay(TimeSpan.FromSeconds(60));
                    return await GetTotalCountFromUserPage(page, url, userName, run);
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
                        Logger.OutputWarning($"Skipping one offer row due to error: {ex.Message}", run.RunId);
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
                Logger.OutputError($"Timeout navigating to URL for {userName} exception: {ex.Message}", run.RunId);
                return (0, 0f);
            }
        }

        public async Task<List<SellerItemsInfo>> getSellerItemsInfo(Sellers seller, IBrowserContext context, Run run)
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
                var (count, avgPrice) = await GetTotalCountFromUserPage(page, rarity.Value, seller.Username, run);
                itemStatsList.Add(new SellerItemsInfo(rarity.Key, count, avgPrice));
            }

            // Optionally store the data in the database
            var dataAccess = new dal();
            await dataAccess.StoreSellerItemInfo(
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
       
        public async Task CollectSellerItems(string userName, IBrowserContext context, Run run)
        {
            var page = await context.NewPageAsync();
            try
            {
                page.SetDefaultTimeout(5000);
                string userDetailsPage = $"https://www.cardmarket.com/it/Pokemon/Users/{userName}";
                var response = await page.GotoAsync(userDetailsPage, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

                if (response == null)
                {
                    Logger.OutputError($"No response received for {userName}.\n", run.RunId);

                    return;
                }

                if (response.Status == 429)
                {
                    Logger.OutputWarning($"HTTP 429 received. Waiting 60 seconds before retrying...", run.RunId);
                    await page.CloseAsync();
                    await Task.Delay(60000); // Wait 1 minute
                    await CollectSellerItems(userName, context, run); // Retry
                    return;
                }

                if (!response.Ok)
                {
                    Logger.OutputError($"HTTP {response.Status} for {userName}: {response.StatusText}\n", run.RunId);

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

                Logger.OutputOk($"Info collected", run.RunId);

            }
            catch (Exception ex)
            {
                Logger.OutputError($"Failed to collect info for {userName}: {ex.Message}", run.RunId);

            }
            finally
            {
                await page.CloseAsync();
            }
        }

        public async Task<bool> GetCardsInfo(IBrowserContext context, Run run)
        {
            var dataAccess = new dal();
            List<Card> cards = await dataAccess.GetAllCardsAsync();
            var page = await context.NewPageAsync();

            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];

                Logger.OutputInfo($"[{(i + 1).ToString().PadLeft(2, '0')}/{cards.Count}] {card.CardName}", run.RunId);
                List<CardsInfo> cardInfoList = new List<CardsInfo>();

                try
                {

                    var cardsInfos = await LoadCardInfoFromUrl(card, card.CardUrlEU, "EU", page, run);
                    cardInfoList.AddRange(cardsInfos);
                    if (card.CardUrlJP is not null)
                    {
                        cardsInfos = await LoadCardInfoFromUrl(card, card.CardUrlJP, "JP", page, run);
                        cardInfoList.AddRange(cardsInfos);
                    }

                    await dataAccess.InsertCardInfo(run.RunId, card.CardID, JsonSerializer.Serialize(cardInfoList).ToString());
                }
                catch
                {
                    Logger.OutputError("LoadCardInfoFromUrl fails", run.RunId);

                }
                Logger.OutputOk($"Completed get {cardInfoList.Count} sellers for this card \n", run.RunId);
            }
            await page.CloseAsync();
            return true;
        }

        public async Task<List<CardsInfo>> LoadCardInfoFromUrl(Card card, string cardUrl, string version, IPage page, Run run)
        {
            await page.GotoAsync(cardUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

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

                    loadMoreButton = await page.QuerySelectorAsync("#loadMoreButton");
                    if (loadMoreButton == null)
                    {
                        break;
                    }

                    await loadMoreButton.ClickAsync();
                    await page.WaitForTimeoutAsync(1000); // Optional: gives time for content to load
                }
                catch (TimeoutException)
                {
                    Logger.OutputInfo("Timeout waiting for #loadMoreButton to become visible", run.RunId);
                    break; 
                }
                catch (PlaywrightException ex)
                {
                    Logger.OutputError($"Click failed on #loadMoreButton: {ex.Message}", run.RunId);
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
                    version,
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

            return cardInfoList;
        }
    
    }
}
