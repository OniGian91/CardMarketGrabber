CREATE TABLE dbo.Cards
(
  [CardID] INT IDENTITY(1,1), 
  [CardName] VARCHAR(MAX),
  [CardUrl] VARCHAR(MAX),
  [Grab] BIT 
  CONSTRAINT PK_Cards_CardID PRIMARY KEY ([CardID]) 
)


