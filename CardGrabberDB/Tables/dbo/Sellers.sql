CREATE TABLE dbo.Sellers
(
  [RunId] INT,
  [Username] VARCHAR(MAX),
  [Type] VARCHAR(MAX),
  [Info] VARCHAR(MAX),
  [CardMarketRank] VARCHAR(MAX),
  [Country] VARCHAR(MAX),
  [Singles] INT,
  [Buy] INT,
  [Sell] INT,
  [SellNotSent] INT,
  [SellNotArrived] INT,
  [BuyNotPayed] INT,
  [BuyNotReceived] INT,
  [InsertDate] DATETIME
)