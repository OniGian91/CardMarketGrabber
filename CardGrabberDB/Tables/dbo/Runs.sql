CREATE TABLE dbo.Runs
(
  [RunId] INT IDENTITY(1,1), 
  [RunIdentifier] UNIQUEIDENTIFIER,
  [Start] DATETIME,
  [End] DATETIME,
  [Status] VARCHAR(MAX),
  [Type] VARCHAR(MAX)
  CONSTRAINT PK_Runs_RunId PRIMARY KEY ([RunId]) 
)
