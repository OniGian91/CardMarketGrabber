CREATE TABLE dbo.Runs
(
  [RunId] INT IDENTITY(1,1), 
  [RunIdentifier] UNIQUEIDENTIFIER,
  [Start] DATETIME,
  [End] DATETIME,
  CONSTRAINT PK_Runs_RunId PRIMARY KEY ([RunId]) 
)
