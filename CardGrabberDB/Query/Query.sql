DECLARE @LastRunIdentifier VARCHAR(MAX);
SELECT TOP 1 @LastRunIdentifier = RunId 
FROM [CardGrabber].[dbo].[ResultsDetailed] 
ORDER BY insertDate DESC;
SELECT ConsideredUsers = COUNT(*) FROM [CardGrabber].[dbo].[ResultsDetailed]  WHERE runId = @LastRunIdentifier
SELECT 
    UserName,
    [Rank] = ROW_NUMBER() OVER (ORDER BY p.MinNonZeroPrice ASC),
    ProfileUrl = REPLACE('https://www.cardmarket.com/it/Pokemon/Users/{userName}/Offers/Singles?maxPrice=0.3&condition=3&idRarity=199&sortBy=price_asc', '{userName}', UserName),
    Tot = DoubleRareItems + UltraRareItems + IllustrationRareItems,
    MinNonZeroPrice = round(p.MinNonZeroPrice,2),
    DoubleRareItems,
    UltraRareItems,
    IllustrationRareItems

FROM 
    [CardGrabber].[dbo].[ResultsDetailed] r
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

--insert into [CardGrabber].[dbo].[users] 
--select distinct Username From [CardGrabber].[dbo].[ResultsDetailed] 


-- SELECT * FROM [CardGrabber].[dbo].[ResultsDetailed] ORDER BY insertDate DESC
-- DELETE FROM [CardGrabber].[dbo].[ResultsDetailed]


DECLARE @LastRunIdentifier VARCHAR(MAX) 
SELECT TOP 1 @LastRunIdentifier = RunId FROM [CardGrabber].[dbo].[ResultsDetailed] ORDER BY insertDate DESC

SELECT 
    --runId,
    UserName,
    ProfileUrl = REPLACE('https://www.cardmarket.com/it/Pokemon/Users/{userName}/Offers/Singles?maxPrice=0.3&condition=3&idRarity=199&sortBy=price_asc', '{userName}', UserName),
    --InsertDate,
    Tot = DoubleRareItems + UltraRareItems + IllustrationRareItems,
    DRItems = DoubleRareItems,
    DRPrice= ROUND(DoubleRareAvgPrice,2),  
    URItems = UltraRareItems,
    URPrice = ROUND(UltraRareAvgPrice,2), 
    ILItems = IllustrationRareItems,
    ILPrice = ROUND(IllustrationRareAvgPrice,2),
    PriceExtimated = 
          (UltraRareItems  * UltraRareAvgPrice) 
        + (DoubleRareItems * DoubleRareAvgPrice)
        + (IllustrationRareItems * IllustrationRareAvgPrice)
FROM 
    [CardGrabber].[dbo].[ResultsDetailed]
WHERE 
    runId = @LastRunIdentifier
ORDER BY 
    Tot DESC

