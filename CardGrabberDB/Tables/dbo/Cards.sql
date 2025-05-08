CREATE TABLE dbo.Cards
(
  [CardID] INT IDENTITY(1,1), 
  [CardName] VARCHAR(MAX),
  [CardUrlEU] VARCHAR(MAX),
  [CardUrlJP] VARCHAR(MAX),
  [CardThumbUrl] VARCHAR(MAX),
  [Grab] BIT 
  CONSTRAINT PK_Cards_CardID PRIMARY KEY ([CardID]) 
)


