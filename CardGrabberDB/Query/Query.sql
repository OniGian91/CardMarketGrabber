SELECT TOP 10 *, TimeTaken = DATEDIFF(MINUTE,[Start],[End]) FROM CardGrabber.dbo.Runs  ORDER BY 1 DESC

DECLARE @LastRunIdentifier INT -- = 'be2f93d8-f76b-41d0-840f-c5f2f8deebbe';
SELECT TOP 1 @LastRunIdentifier = RunId 
FROM CardGrabber.dbo.Runs 
ORDER BY [Start] DESC


select UserBase = COUNT(DISTINCT [UserName]) From [CardGrabber].[dbo].Sellers 

;WITH RankedSellers AS (
    SELECT 
        [Username],
        [Type],
        [Info],
        [CardMarketRank],
        [Country],
        [Singles],
        [Buy],
        [Sell],
        [SellNotSent],
        [SellNotArrived],
        [BuyNotPayed],
        [BuyNotReceived],
        [InsertDate],
        ROW_NUMBER() OVER (PARTITION BY Username ORDER BY InsertDate DESC) AS rn,
        COUNT(*) OVER (PARTITION BY Username) AS [Versions],
        'https://www.cardmarket.com/it/Pokemon/Users/' + [Username] AS UserProfileLink
    FROM [CardGrabber].[dbo].[Sellers]
)
SELECT 
    [Username],
    [Type],
    [StartYear] = [Info],
    [CardMarketRank],
    [Country],
    [Singles],
    [Buy],
    [Sell],
    [SellNotSent],
    [SellNotArrived],
    [BuyNotPayed],
    [BuyNotReceived],
    LastUpdate = [InsertDate],
    [Versions],
    UserProfileLink
FROM RankedSellers
WHERE rn = 1



SELECT 
    UserName,
    [Rank] = ROW_NUMBER() OVER (ORDER BY p.MinNonZeroPrice ASC),
    ProfileUrl = REPLACE('https://www.cardmarket.com/it/Pokemon/Users/{userName}/Offers/Singles?maxPrice=0.3&condition=3&idRarity=199&sortBy=price_asc', '{userName}', UserName),
    Tot = DoubleRareItems + UltraRareItems + IllustrationRareItems,
    MinNonZeroPrice = round(p.MinNonZeroPrice,2),
    DoubleRareItems,
    UltraRareItems,
    holoRareItems,
    IllustrationRareItems

FROM 
    [CardGrabber].[dbo].[SellerItemsInfo] r
CROSS APPLY 
(
    SELECT MIN(val) AS MinNonZeroPrice
    FROM (VALUES 
        (r.DoubleRareAvgPrice), 
        (r.UltraRareAvgPrice), 
        (r.IllustrationRareAvgPrice)
    ) AS prices(val)
    WHERE val > 0
) p
WHERE 
    r.runId = @LastRunIdentifier
    AND (
        r.DoubleRareAvgPrice > 0 OR 
        r.UltraRareAvgPrice > 0 OR 
        r.IllustrationRareAvgPrice > 0
    )
    AND DoubleRareItems + UltraRareItems + IllustrationRareItems > 40
ORDER BY p.MinNonZeroPrice ASC;
