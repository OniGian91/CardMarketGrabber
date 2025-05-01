CREATE TABLE dbo.Logs
(
  [LogId] INT IDENTITY(1,1), 
  [RunId] INT,
  [Severity] TINYINT,
  [LogDate] DATETIME,
  [Message] VARCHAR(MAX),
  CONSTRAINT PK_Logs_LogId PRIMARY KEY ([LogId]) 
)


